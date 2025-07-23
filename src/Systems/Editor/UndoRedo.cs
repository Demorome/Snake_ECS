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
        // For Ctrl+Z 'Undo' feature.
    public static Stack<(Entity, dynamic, bool)> ChangeHistory = new();
    // For Ctrl+Y 'Redo' feature.
    public static Stack<(Entity, dynamic, bool)> UndoHistory = new();

    public static void StoreEntityDestroyHistory(Entity entity, World world)
    {
        StoreEntityComponents(entity, world, ChangeHistory, true);
    }
    public static void StoreEntityCreateHistory(Entity entity, World world)
    {
        StoreEntityComponents(entity, world, ChangeHistory, false);
    }

    static void StoreEntityComponents(
        Entity entity,
        World world,
        Stack<(Entity, dynamic, bool)> ToSaveComponents,
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

        ToSaveComponents.Push((entity, components, willBeDestroyed));
    }

    public static void ClearRedoList()
    {
        if (UndoHistory.Count != 0)
        {
            Logger.LogInfo("Cleared Redo list.");
            UndoHistory.Clear();
        }
    }

    static void UndoRedoLastComponentChange(
        World world,
        Stack<(Entity, dynamic, bool)> ToRestore,
        Stack<(Entity, dynamic, bool)> ToRememberRestore,
        bool isUndoOrRedo
        )
    {
        if (ToRestore.Count == 0)
        {
            return;
        }

        // If componentExisted == false, then `componentPriorToChange` will be a default-instantiated dummy component.
        // `componentPriorToChange` will never be null. FIXME: Enforce this somehow?
        var (entity, componentPriorToChange, hadComponent) = ToRestore.Pop();

        // Handle entity deletion case.
        if (componentPriorToChange.GetType() == typeof(List<dynamic>))
        {
            string entityString;

            if (hadComponent)
            {
                // Entity was deleted; recreate it along with all of its components
                var componentList = componentPriorToChange as List<dynamic>;
                var oldTag = componentList[componentList.Count - 1] as string;
                entity = world.CreateEntity(oldTag);
                componentList.RemoveAt(componentList.Count - 1);

                foreach (var component in componentList)
                {
                    DynamicComponentManip.Set(world, entity, component);
                }

                componentList.Clear();
                ToRememberRestore.Push((entity, componentList, false));
                entityString = EditorSystem.EntityToString(world, entity);
            }
            else
            {
                entityString = EditorSystem.EntityToString(world, entity);

                // Entity was un-deleted; re-delete it.
                StoreEntityComponents(entity, world, ToRememberRestore, true);
                world.Destroy(entity);
            }

            Logger.LogInfo($"{(!isUndoOrRedo ? "Undid" : "Redid")} {entityString}'s deletion.");

            return;
        }

        // Handle single component change case.
        Logger.LogInfo($"{(!isUndoOrRedo ? "Undid" : "Redid")} change to {EditorSystem.EntityToString(world, entity)} for {componentPriorToChange.GetType().Name} : Reset to {componentPriorToChange.ToString()}");


        // Store current state so we can potentially 'Redo' this 'Undo' change.
        if (!DynamicComponentManip.Has(world, entity, componentPriorToChange))
        {
            var type = componentPriorToChange.GetType();
            var dummyComponent = (dynamic)Activator.CreateInstance(type);
            ToRememberRestore.Push((entity, dummyComponent, false));
        }
        else
        {
            // Component didn't exist before the change, so it's safe to assume it must exist now.
            var componentPriorToUndo = DynamicComponentManip.Get(world, entity, componentPriorToChange);
            ToRememberRestore.Push((entity, componentPriorToUndo, true));
        }

        // Undo the change.
        if (!hadComponent)
        {
            DynamicComponentManip.Remove(world, entity, componentPriorToChange);
        }
        else
        {
            DynamicComponentManip.Set(world, entity, componentPriorToChange);
        }
    }

    public static void UndoLastComponentChange(World world)
    {
        UndoRedoLastComponentChange(world, ChangeHistory, UndoHistory, false);
    }

    public static void RedoLastComponentChange(World world)
    {
        UndoRedoLastComponentChange(world, UndoHistory, ChangeHistory, true);
    }
}

#endif