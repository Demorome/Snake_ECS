#if DEBUG

using System;
using System.Collections.Generic;
using System.Reflection;
using MoonTools.ECS;
using MoonWorks;
using RollAndCash.Systems;

namespace RollAndCash.Editor;

// TODO: Make changes history contextual to separate tabs, instead of global?
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

        Placeholder // to wait to determine what the type of the change will be, usually to prep a redo.
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

    public static void BeginGroupedChange(
        bool isForUndoOrRedo = false // for Undo by default
        )
    {
        var ToChange = !isForUndoOrRedo ? ChangeHistory : UndoHistory;

        if (ActiveGroupedChangesCount == 0)
        {
            ToChange.Push((new List<object>(), new List<dynamic>(), ChangeType.Placeholder));
        }
        // Else, group together nested grouped changes as one group.

        ActiveGroupedChangesCount += 1;
    }

    public static void EndGroupedChange(bool isForUndoOrRedo = false)
    {
        ActiveGroupedChangesCount -= 1;
        if (ActiveGroupedChangesCount < 0)
        {
            throw new Exception("Nothing to end!");
        }

        var ToChange = !isForUndoOrRedo ? ChangeHistory : UndoHistory;

        // Clean up empty grouped change, if needed.
        // For nested group changes, wait until end of nesting to make a determination.
        if (ActiveGroupedChangesCount == 0)
        {
            var (subject, _, _) = ToChange.Peek();
            if (((List<object>)subject).Count == 0)
            {
                ToChange.Pop();
            }
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
    public static void ClearChangeHistoryList()
    {
        if (ChangeHistory.Count != 0)
        {
            Logger.LogInfo("Cleared Changes list.");
            ChangeHistory.Clear();
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

    // For Ctrl+Z 'Undo' feature.
    private static Stack<(object ChangeSubject, dynamic Value, ChangeType)>
        ChangeHistory = new();

    // For Ctrl+Y 'Redo' feature.
    private static Stack<(object ChangeSubject, dynamic Value, ChangeType)>
        UndoHistory = new();

    private static int ActiveGroupedChangesCount = 0;

    private static void PushChange(
        Stack<(object, dynamic, ChangeType)> ToChange,
        object subject,
        dynamic value,
        ChangeType changeToUndo
        )
    {
        if (ActiveGroupedChangesCount != 0)
        {
            var (changeSubject, changeValue, changeToUndo_Cached) = ToChange.Pop();
            var changeSubjects = (List<object>)changeSubject;
            var changeValues = (List<dynamic>)changeValue;

            if (changeToUndo_Cached == ChangeType.Placeholder)
            {
                changeToUndo_Cached = changeToUndo;
            }
            else if (changeToUndo_Cached != changeToUndo)
            {
                throw new Exception("Grouped changes must be of the same type!");
            }

            changeSubjects.Add(subject);
            changeValues.Add(value);

            // Just to update the value of changeToUndo_Cached, in case it was a placeholder.
            // Very inneficient but whatever, it's editor code.
            ToChange.Push((changeSubjects, changeValues, changeToUndo_Cached));
        }
        else
        {
            ToChange.Push(
                (subject, value, changeToUndo)
            );
        }
    }

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
            var component = (dynamic)genericGetComponentStorageMethod.Invoke(world, [entity])!;
            components.Add(component);
        }

        // Also store the tag
        components.Add(world.GetTag(entity));

        Logger.LogInfo((willBeDestroyed ? "Destroyed" : "Created") + $" {EditorSystem.EntityToString(world, entity)}");

        var changeToUndo = willBeDestroyed ? ChangeType.Entity_Deletion : ChangeType.Entity_Creation;
        PushChange(ToAllowUndo, entity, components, changeToUndo);
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
            var changeSubjects = (List<object>)changeSubject;
            var changeValues = (List<dynamic>)changeValue;

            // Group the Redo changes together as well.
            BeginGroupedChange(!isUndoOrRedo);
            var i = 0;
            foreach (var subject in changeSubjects)
            {
                UndoRedo_SingleChange(
                    subject, changeValues[i], changeToUndo,
                    world, ToUndoUndo, isUndoOrRedo
                );
                ++i;
            }
            EndGroupedChange(!isUndoOrRedo);
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
                    var oldTag = componentList![componentList.Count - 1] as string;
                    entity = world.CreateEntity(oldTag!);
                    componentList.RemoveAt(componentList.Count - 1);

                    foreach (var component in componentList)
                    {
                        DynamicComponentManip.Set(world, entity, component);
                    }

                    componentList.Clear();

                    PushChange(ToUndoUndo, entity, componentList, ChangeType.Entity_Creation);

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
                PushChange(ToUndoUndo, entity, dummyComponent, ChangeType.Entity_Component_Add);
            }
            else if (changeToUndo == ChangeType.Entity_Component_Add
                || changeToUndo == ChangeType.Entity_Component_Modify)
            {
                var componentPriorToUndo = DynamicComponentManip.Get(world, entity, componentChanges);
                PushChange(ToUndoUndo, entity, componentPriorToUndo,
                    changeToUndo == ChangeType.Entity_Component_Add ?
                            ChangeType.Entity_Component_Remove : ChangeType.Entity_Component_Modify
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