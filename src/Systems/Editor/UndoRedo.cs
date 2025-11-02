#if DEBUG

using System;
using System.Collections.Generic;
using System.Reflection;
using MoonTools.ECS;
using MoonWorks;
using RollAndCash.Systems;

namespace RollAndCash.Editor;

public static class UndoRedo
{
    public enum ChangeType
    {
        Entity_Creation = 0,
        Entity_Deletion,
        Entity_Component_Modify,
        Entity_Component_Remove,
        Entity_Component_Add,
        ENTITY_CHANGES_COUNT,

        // TODO: Level layer changes
    }

    public static void RememberEntityDestruction(Entity entity, World world)
    {
        StoreEntityComponents(entity, world, ChangeHistory, true);
    }
    public static void RememberEntityCreation(Entity entity, World world)
    {
        StoreEntityComponents(entity, world, ChangeHistory, false);
    }

    // TODO: Support component addition/removal, when needed.
    public static void RememberEntityComponentChange(Entity entity, World world, dynamic component)
    {
        ChangeHistory.Push(
            (
                entity, component, ChangeType.Entity_Component_Modify
            )
        );
        Logger.LogInfo($"Stored prior state for {EditorSystem.EntityToString(world, entity)}'s {component.GetType()}: {component}");
    }

    public static void StartGroupedChange(ChangeType changeType)
    {
        ChangeHistory.Push((new List<object>(), new List<dynamic>(), changeType));
        ActiveGroupedChangesCount += 1;
    }
    public static void EndGroupedChange()
    {
        ActiveGroupedChangesCount -= 1;
        if (ActiveGroupedChangesCount < 0)
        {
            throw new Exception("Nothing to end!");
        }

        // Clean up empty grouped change, if needed.
        var (subject, _, _) = ChangeHistory.Peek();
        if (((List<object>)subject).Count == 0)
        {
            ChangeHistory.Pop();
        }
    }

    public static void UndoLastChange(World world)
    {
        UndoRedoLastChange(world, ChangeHistory, UndoHistory, false);
    }
    public static void RedoLastChange(World world)
    {
        UndoRedoLastChange(world, UndoHistory, ChangeHistory, true);
    }
    public static void ClearRedoList()
    {
        if (UndoHistory.Count != 0)
        {
            Logger.LogInfo("Cleared Redo list.");
            UndoHistory.Clear();
        }
    }

    public static bool HasChangesToUndo()
    {
        return ChangeHistory.Count != 0;
    }
    public static bool HasChangesToRedo()
    {
        return UndoHistory.Count != 0;
    }

    //======= Private

    private static int ActiveGroupedChangesCount = 0;

    // For Ctrl+Z 'Undo' feature.
    private static Stack<(object ChangeSubject, dynamic Value, ChangeType)>
        ChangeHistory = new();
        
    // For Ctrl+Y 'Redo' feature.
    private static Stack<(object ChangeSubject, dynamic Value, ChangeType)> 
        UndoHistory = new();

    private static void StoreEntityComponents(
        Entity entity,
        World world,
        Stack<(object, dynamic, ChangeType)> ToAllowUndo,
        bool willBeDestroyed // else, wasCreated
        )
    {
        var components = new List<dynamic>();

        foreach (var componentType in world.Debug_GetAllComponentTypes(entity))
        {
            var baseGetComponentMethod = typeof(World).GetMethod(nameof(World.Get), BindingFlags.Public | BindingFlags.Instance)!;
            var genericGetComponentStorageMethod = baseGetComponentMethod.MakeGenericMethod(componentType);
            var component = (dynamic)genericGetComponentStorageMethod.Invoke(world, [entity]);
            components.Add(component);
        }

        // Also store the tag
        components.Add(world.GetTag(entity));

        var changeToUndo = willBeDestroyed ? ChangeType.Entity_Deletion : ChangeType.Entity_Creation;

        Logger.LogInfo((willBeDestroyed ? "Destroyed" : "Created") + $" {EditorSystem.EntityToString(world, entity)}");

        if (ActiveGroupedChangesCount != 0)
        {
            var (changeSubject, changeValue, changeToUndo_Cached) = ToAllowUndo.Peek();
            var changeSubjects = (List<object>)changeSubject;
            var changeValues = (List<dynamic>)changeValue;

            if (changeToUndo_Cached != changeToUndo)
            {
                throw new Exception("Grouped changes must be of the same type!");
            }

            changeSubjects.Add(entity);
            changeValues.Add(components);
        }
        else
        {
            ToAllowUndo.Push(
                ( entity, components, changeToUndo )
            );
        }
    }

    private static bool IsChangeEntityRelated(ChangeType change)
    {
        return change < ChangeType.ENTITY_CHANGES_COUNT;
    }

    private static void UndoRedoLastChange(
        World world,
        Stack<(object, dynamic, ChangeType)> ToUndo,
        Stack<(object, dynamic, ChangeType)> ToUndoUndo,
        bool isUndoOrRedo
        )
    {
        if (ToUndo.Count == 0)
        {
            return;
        }

        var (changeSubject, changeValue, changeToUndo) = ToUndo.Pop();

        // Repeat a change multiple times if there's multiple subjects
        if (changeSubject.GetType() == typeof(List<object>))
        {
            var i = 0;
            var changeSubjects = (List<object>)changeSubject;
            var changeValues = (List<dynamic>)changeValue;
            foreach (var subject in changeSubjects)
            {
                UndoRedo_SingleChange(
                    subject, changeValues[i], changeToUndo,
                    world, ToUndoUndo, isUndoOrRedo
                );
                ++i;
            }
        }
        else
        {
            UndoRedo_SingleChange(changeSubject, changeValue, changeToUndo,
                world, ToUndoUndo, isUndoOrRedo);
        }
    }
    
    private static void UndoRedo_SingleChange(
        object changeSubject,
        dynamic changeValue,
        ChangeType changeToUndo,
        World world,
        Stack<(object, dynamic, ChangeType)> ToUndoUndo,
        bool isUndoOrRedo
    )
    {
        if (IsChangeEntityRelated(changeToUndo))
        {
            var entity = (Entity)changeSubject;
            var componentChanges = changeValue;

            // Handle entity creation & deletion case.
            if (componentChanges.GetType() == typeof(List<dynamic>))
            {
                string entityString;

                if (changeToUndo == ChangeType.Entity_Deletion)
                {
                    // Recreate it along with all of its components
                    var componentList = componentChanges as List<dynamic>;
                    var oldTag = componentList[componentList.Count - 1] as string;
                    entity = world.CreateEntity(oldTag);
                    componentList.RemoveAt(componentList.Count - 1);

                    foreach (var component in componentList)
                    {
                        DynamicComponentManip.Set(world, entity, component);
                    }

                    componentList.Clear();
                    ToUndoUndo.Push((entity, componentList, ChangeType.Entity_Creation));
                    entityString = EditorSystem.EntityToString(world, entity);
                }
                else if (changeToUndo == ChangeType.Entity_Creation)
                {
                    // Undo the creation by deleting it.
                    entityString = EditorSystem.EntityToString(world, entity);
                    StoreEntityComponents(entity, world, ToUndoUndo, true);
                    world.Destroy(entity);
                }
                else
                {
                    throw new Exception("What??");
                }

                Logger.LogInfo($"{(!isUndoOrRedo ? "Undid" : "Redid")} {entityString}'s deletion.");

                return;
            }
            // Else, handle single component change case.

            Logger.LogInfo($"{(!isUndoOrRedo ? "Undid" : "Redid")} change to {EditorSystem.EntityToString(world, entity)} for {componentChanges.GetType().Name} : Reset to {componentChanges.ToString()}");

            // Store current state so we can potentially 'Redo' this 'Undo' change, and vice-versa.
            if (changeToUndo == ChangeType.Entity_Component_Remove)
            {
                var type = componentChanges.GetType();
                var dummyComponent = (dynamic)Activator.CreateInstance(type);
                ToUndoUndo.Push(
                    (
                        entity, dummyComponent,
                        ChangeType.Entity_Component_Add // since we'll be re-adding the component below, to undo its removal.
                    )
                );
            }
            else if (changeToUndo == ChangeType.Entity_Component_Add
                || changeToUndo == ChangeType.Entity_Component_Modify)
            {
                var componentPriorToUndo = DynamicComponentManip.Get(world, entity, componentChanges);
                ToUndoUndo.Push(
                    (
                        entity, componentPriorToUndo,
                        changeToUndo == ChangeType.Entity_Component_Add ?
                            ChangeType.Entity_Component_Remove : ChangeType.Entity_Component_Modify
                    )
                );
            }

            // Undo the change by performing the opposite operation.
            if (changeToUndo == ChangeType.Entity_Component_Add)
            {
                DynamicComponentManip.Remove(world, entity, componentChanges);
            }
            else
            {
                DynamicComponentManip.Set(world, entity, componentChanges);
            }
        }
        else
        {
            // TODO!   
        }
    }
}

#endif