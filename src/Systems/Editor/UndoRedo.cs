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

    // For Ctrl+Z 'Undo' feature.
    public static Stack<(object ChangeSubject, dynamic Value, ChangeType)>
        ChangeHistory { get; private set; } = new();
        
    // For Ctrl+Y 'Redo' feature.
    public static Stack<(object ChangeSubject, dynamic Value, ChangeType)> 
        UndoHistory { get; private set; } = new();

    public static void RememberEntityDestruction(Entity entity, World world)
    {
        StoreEntityComponents(entity, world, ChangeHistory, true);
    }
    public static void RememberEntityCreation(Entity entity, World world)
    {
        StoreEntityComponents(entity, world, ChangeHistory, false);
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

        var changeToUndo = willBeDestroyed ? ChangeType.Entity_Creation : ChangeType.Entity_Deletion;

        ToAllowUndo.Push(
            ( entity, components, changeToUndo )
        );
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
        if (changeSubject.GetType() == typeof(List<Object>))
        {
            var changeSubjects = (List<Object>)changeSubject;
            foreach (var subject in changeSubjects)
            {
                UndoRedo_SingleChange(subject, changeValue, changeToUndo,
                    world, ToUndoUndo, isUndoOrRedo);
            }
        }
        else
        {
            UndoRedo_SingleChange(changeSubject, changeValue, changeToUndo,
                world, ToUndoUndo, isUndoOrRedo);
        }
    }
    
    private static void UndoRedo_SingleChange(
        Object changeSubject,
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

            // Handle entity deletion case.
            if (componentChanges.GetType() == typeof(List<dynamic>))
            {
                string entityString;

                if (changeToUndo == ChangeType.Entity_Creation)
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
                    ToUndoUndo.Push((entity, componentList, ChangeType.Entity_Deletion));
                    entityString = EditorSystem.EntityToString(world, entity);
                }
                else if (changeToUndo == ChangeType.Entity_Deletion)
                {
                    // Entity was un-deleted; re-delete it.
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
            if (changeToUndo == ChangeType.Entity_Component_Add)
            {
                var type = componentChanges.GetType();
                var dummyComponent = (dynamic)Activator.CreateInstance(type);
                ToUndoUndo.Push(
                    (
                        entity, dummyComponent,
                        ChangeType.Entity_Component_Remove
                    )
                );
            }
            else if (changeToUndo == ChangeType.Entity_Component_Remove
                || changeToUndo == ChangeType.Entity_Component_Modify)
            {
                var componentPriorToUndo = DynamicComponentManip.Get(world, entity, componentChanges);
                ToUndoUndo.Push(
                    (
                        entity, componentPriorToUndo,
                        changeToUndo == ChangeType.Entity_Component_Remove ?
                            ChangeType.Entity_Component_Add : ChangeType.Entity_Component_Modify
                    )
                );
            }

            // Apply the change to undo the previous change; our changeType is the opposite.
            if (changeToUndo == ChangeType.Entity_Component_Remove)
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