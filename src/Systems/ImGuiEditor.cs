#if DEBUG

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Unicode;
using ImGuiNET;
using Microsoft.VisualBasic;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.AsyncIO;
using MoonWorks.Graphics;
using MoonWorks.Input;
using MoonWorks.Math;
using RollAndCash.Components;
using RollAndCash.Data;
using RollAndCash.GameStates;
using RollAndCash.Relations;
using RollAndCash.Systems;
using RollAndCash.Utility;
using SDL3;
using Buffer = MoonWorks.Graphics.Buffer;

namespace RollAndCash.Systems;

// You better not be pronouncing ImGui as "I'm Gooey"... :^)
public class ImGuiEditor : MoonTools.ECS.System
{
    static List<Type> ComponentTypes = new();

    public static void StaticInit()
    {
        // FIXME: Update on hot-reload, if we add new component types?
        InitComponentTypesList();
    }

    TileManipulator TileManipulator;

    MoonTools.ECS.Filter PositionFilter;

    public Entity? DebugEntity = null; // So we can stick Relations on this to safely track other entities.
    static string DebugEntityTag = "EDITOR";

    public ImGuiEditor(World world) : base(world)
    {
        PositionFilter = FilterBuilder.Include<Position2D>().Build();
    }

    public override void Update(TimeSpan delta)
    {
        if (!DebugEntity.HasValue)
        {
            DebugEntity = World.CreateEntity(DebugEntityTag);
            Set(DebugEntity.Value, new Editor_DontShow());
        }

        DrawHelpWindow(World);
        HandleDebugKeybinds(World);
        DrawDetachedWindows(World);
        DrawEntitiesWithComponentWindows(World);

        HandleSelectionMode();
        HandleLevelEditor();
	}

    class DebugAction
    {
        public DebugAction(string name, Action<World> action, bool opensWindow = false)
        {
            WorldAction = action;
            Name = name;
            OpensWindow = opensWindow;
        }
        public DebugAction(string name, Func<bool> func)
        {
            ToggleFunc = func;
            Name = name;
        }

        public string Name;
        public Action<World> WorldAction = null;
        public Func<bool> ToggleFunc = null;
        public bool OpensWindow = false;

        public bool? Invoke(World world)
        {
            if (WorldAction == null)
            {
                return ToggleFunc();
            }
            else
            {
                if (OpensWindow)
                {
                    DetachedWindows.TryAdd(Name, WorldAction);
                }
                else
                {
                    WorldAction(world);
                }
                return null;
            }
        }
    };

    static Dictionary<ImGuiKey, DebugAction> DebugKeybinds = new()
    {
        { ImGuiKey.F1,                   new("Search By Component", DrawComponentTypeSearch, true)},
        { ImGuiKey.ModCtrl | ImGuiKey.T, new("Show Colliders",
            () => { return Renderer.DrawDebugColliders = !Renderer.DrawDebugColliders; } )
        },
        { ImGuiKey.F6,                   new("Toggle Freeze All",
            () => { return GameplayState.FreezeTimeForAll = !GameplayState.FreezeTimeForAll; } )
        },
        { ImGuiKey.ModCtrl | ImGuiKey.Z, new("Undo", UndoLastComponentChange) },
        { ImGuiKey.ModCtrl | ImGuiKey.Y, new("Redo", RedoLastComponentChange) },
        { ImGuiKey.MouseX2,              new("Toggle Selection Mode",
             () => { return IsInEntitySelectionMode = !IsInEntitySelectionMode; } )
        },
        { ImGuiKey.None,                 new("Toggle Level Editor",
            () => { return IsInLevelEditor = !IsInLevelEditor; } )
        },
        { ImGuiKey.F2,                   new("Prefabs", ShowPrefabSpawnerWindow, true )},
    };

    static void ShowPrefabSpawnerWindow(World world)
    {
        /*if (ImGui.Button())
        {

        }*/
        // TODO: Once button to spawn a prefab entity is pressed, make it appear transparent below cursor.
        // TODO: Pressing click will spawn it.
        // TODO: If spawned, add to change history.
    }

    public static SpriteAnimationInfo SelectedTileSpriteToDraw = null;

    static void ShowTileSelectionMenu(List<SpriteAnimationInfo> tileSet)
    {
        var imageSize = Dimensions.TILE_DIMENSIONS;
        foreach (var tileSprite in tileSet)
        {
            /*if (ImGuiExtensions.ImageButton())
            {
                // TODO: Show that tile is selected + store that

            }*/
        }

    }

    class TileLayer
    {
        public TileLayer(string layerName, Action<World> showTilePicker)
        {
            Name = layerName;
            ShowTilePicker = showTilePicker;
        }
        public string Name;
        public Action<World> ShowTilePicker;
        public bool IsVisible = true;
    }

    static void ShowSolidTiles(World world)
    {
        //ShowTileSelectionMenu(??); // FIXME:
    }

    static void ShowBackgroundTiles(World world)
    {
        //ShowTileSelectionMenu(??);
    }

    List<TileLayer> TileLayers = new()
    {
        new ("Solid Tiles", ShowSolidTiles),
        new ("Background Tiles", ShowBackgroundTiles)
    };

    static bool SnapToGrid = true;

    void ShowTileLayerOptions()
    {
        // TODO: Snap to grid option? Not sure if I should support going off-grid yet.
        if (ImGui.Begin("Tile Layers"))
        {
            foreach (var tileLayer in TileLayers)
            {
                if (ImGui.Checkbox("##" + tileLayer.Name + "Visibility", ref tileLayer.IsVisible))
                {
                    // TODO: Hide the sprites on that layer if the layer is checked off.
                }
                ImGui.SameLine();
                if (ImGui.Button(tileLayer.Name))
                {
                    DetachedWindows.Add(tileLayer.Name, tileLayer.ShowTilePicker);
                }
            }
        }
    }

    Vector2? GetTilePos(Position2D worldPos)
    {
        var tilePos = new Vector2(worldPos.X / Dimensions.TILE_SIZE, worldPos.Y / Dimensions.TILE_SIZE);
        if (tilePos.X < 0 || tilePos.X >= Dimensions.TILE_COLUMN_COUNT)
        {
            return null;
        }
        else if (tilePos.Y < 0 || tilePos.Y >= Dimensions.TILE_ROW_COUNT)
        {
            return null;
        }
        return tilePos;
    }

    public static bool IsInLevelEditor = false;

    public Vector2? HoveredOverTile = null;

    void HandleLevelEditor()
    {
        if (!IsInLevelEditor)
        {
            return;
        }

        ShowTileLayerOptions();

        var mouseHoveringOverAnyWindow = ImGui.GetIO().WantCaptureMouse;
        if (!mouseHoveringOverAnyWindow)
        {
            // TODO: Get nearest tile to paint in
            var worldMousePosition = Input.WorldMousePosition;
            HoveredOverTile = GetTilePos(worldMousePosition);
            if (HoveredOverTile.HasValue)
            {
                if (SelectedTileSpriteToDraw != null)
                {
                    // TODO: Painting the tiles to the world!
                    // TODO: Don't spawn anything if tile is already painted in (at that tile depth; allow BG tiles for example??)
                    //TileManipulator.SpawnSolidTile(TODO, SelectedTileSprite);
                }
            }
        }
    }

    static SpatialHash<Entity> VisualEntitiesSpatialHash =
        new SpatialHash<Entity>(0, 0, Dimensions.GAME_W, Dimensions.GAME_H, 32);

    void HandleSelectionMode()
    {
        VisualEntitiesSpatialHash.Clear();

        var mouseWorldPos = Input.WorldMousePosition;
        var mouseHitboxRect = new Rectangle(0, 0, 1, 1);
        var mouseWorldPosRect = mouseHitboxRect.GetWorldRect(mouseWorldPos);

        var mouseHoveringOverAnyWindow = ImGui.GetIO().WantCaptureMouse;

        Entity? maybeSelectedEntity = null;

        if (IsInEntitySelectionMode)
        {
            UnrelateAll<Editor_SelectedEntity>(DebugEntity.Value);
            if (mouseHoveringOverAnyWindow)
            {
                return;
            }

            foreach (var entity in PositionFilter.Entities)
            {
                var rect = GetEntityVisualRect(entity);
                if (rect.HasValue)
                {
                    var worldRect = rect.Value.GetWorldRect(Get<Position2D>(entity));
                    VisualEntitiesSpatialHash.Insert(entity, worldRect);
                }
            }

            List<Entity> hoveredOverEntities = new();

            foreach (var (entity, rect) in VisualEntitiesSpatialHash.Retrieve(mouseWorldPosRect))
            {
                if (mouseWorldPosRect.Intersects(rect))
                {
                    hoveredOverEntities.Add(entity);
                }
            }

            if (hoveredOverEntities.Count == 0)
            {
                return;
            }

            // Sort by Depth
            hoveredOverEntities.Sort((Entity A, Entity B) =>
                {
                    var depthA = Has<Depth>(A) ? Get<Depth>(A).Value : 2f;
                    var depthB = Has<Depth>(B) ? Get<Depth>(B).Value : 2f;
                    return depthA.CompareTo(depthB);
                }
            );

            // We'll consider this the "selected" entity.
            var hoveredOverEntity = hoveredOverEntities[0];
            maybeSelectedEntity = hoveredOverEntity;

            // Exit selection mode if we confirm our selection.
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                IsInEntitySelectionMode = false;
                Logger.LogInfo($"Selected {EntityToString(hoveredOverEntity)}");
            }
            // Switch selection to one of greater/lower depth at the same mouse position.
            else if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            {
                // FIXME:
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            {
                // FIXME:
            }

            Relate(DebugEntity.Value, hoveredOverEntity, new Editor_SelectedEntity());
        }
        else
        {
            maybeSelectedEntity = GetSelectedEntity();
            if (maybeSelectedEntity.HasValue)
            {
                var selectedEntity = maybeSelectedEntity.Value;

                // Check if user unselects the entity by clicking away from it.
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !mouseHoveringOverAnyWindow)
                {
                    var selectedRect = GetEntityVisualRect(selectedEntity);
                    if (selectedRect.HasValue)
                    {
                        var worldRect = selectedRect.Value.GetWorldRect(Get<Position2D>(selectedEntity));
                        VisualEntitiesSpatialHash.Insert(selectedEntity, worldRect);
                    }

                    bool unselect = true;

                    foreach (var (entity, rect) in VisualEntitiesSpatialHash.Retrieve(mouseWorldPosRect))
                    {
                        if (mouseWorldPosRect.Intersects(rect))
                        {
                            unselect = false;
                        }
                    }

                    if (unselect)
                    {
                        UnrelateAll<Editor_SelectedEntity>(DebugEntity.Value);
                    }
                }
            }
        }

        if (maybeSelectedEntity.HasValue)
        {
            var selectedEntity = maybeSelectedEntity.Value;
            if (!mouseHoveringOverAnyWindow)
            {
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    DetachedWindows.TryAdd(EntityToString(selectedEntity), selectedEntity);
                }

                if (ImGui.IsKeyDown(ImGuiKey.Delete))
                {
                    Logger.LogInfo($"Deleted {EntityToString(selectedEntity)}");
                    StoreEntityComponentsBeforeDestroy(selectedEntity, World, ComponentChangeHistory);
                    Destroy(selectedEntity);
                    ClearRedoList();
                }
            }

        }
    }

    public Entity? GetSelectedEntity()
    {
        if (DebugEntity.HasValue)
        {
            var debugEntity = DebugEntity.Value;
            if (HasOutRelation<Editor_SelectedEntity>(debugEntity))
            {
                return OutRelationSingleton<Editor_SelectedEntity>(debugEntity);
            }
        }
        return null;
    }

    public Rectangle? GetEntityVisualRect(Entity entity)
    {
        if (Has<Rectangle>(entity) && Has<DrawAsRectangle>(entity))
        {
            return Get<Rectangle>(entity);
        }
        else if (Has<SpriteAnimation>(entity))
        {
            var spriteAnim = Get<SpriteAnimation>(entity);
            var rect = spriteAnim.CurrentSprite.FrameRect;
			return new Rectangle(rect.X - rect.W / 2, rect.Y - rect.H / 2, rect.W, rect.H);
        }
        return null;
    }

    static void InitComponentTypesList()
    {
        string namespaceFilter = nameof(RollAndCash) + '.' + nameof(Components);

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (!type.IsValueType || type.Namespace != namespaceFilter)
            {
                continue;
            }

            ComponentTypes.Add(type);
        }

        ComponentTypes.Sort((Type A, Type B) => { return A.Name.CompareTo(B.Name); });
    }

    public static string EntityToString(World world, Entity e)
    {
        var tag = world.GetTag(e);
        if (tag.Length == 0)
        {
            return e.ToString();
        }
        return $"Entity {{ ID = {e.ID}, Tag = {tag} }}";
    }

    public string EntityToString(Entity e)
    {
        var tag = World.GetTag(e);
        if (tag.Length == 0)
        {
            return e.ToString();
        }
        return $"Entity {{ ID = {e.ID}, Tag = {tag} }}";
    }

    public static bool IsInEntitySelectionMode = false;

    // For Ctrl+Z 'Undo' feature.
    static Stack<(Entity, dynamic, bool)> ComponentChangeHistory = new();
    static Stack<(Entity, dynamic, bool)> UndoHistory = new();

    static void StoreEntityComponentsBeforeDestroy(
        Entity entity,
        World world,
        Stack<(Entity, dynamic, bool)> ToSaveComponents)
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

        ToSaveComponents.Push((entity, components, true));
    }

    // For some reason, can't directly pass a `dynamic` value to an "in" param, so we use this.
    static void WorkaroundSet<T>(World world, Entity entity, T component) where T : unmanaged
    {
        world.Set(entity, component);
    }

    // Need to pass dummy typed component to extract the T type from the `dynamic` value.
    static void WorkaroundRemove<T>(World world, Entity entity, T dummyComponent) where T : unmanaged
    {
        world.Remove<T>(entity);
    }
    static T WorkaroundGet<T>(World world, Entity entity, T dummyComponent) where T : unmanaged
    {
        return world.Get<T>(entity);
    }
    static bool WorkaroundHas<T>(World world, Entity entity, T dummyComponent) where T : unmanaged
    {
        return world.Has<T>(entity);
    }

    static void ClearRedoList()
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
                    WorkaroundSet(world, entity, component);
                }

                componentList.Clear();
                ToRememberRestore.Push((entity, componentList, false));
                entityString = EntityToString(world, entity);
            }
            else
            {
                entityString = EntityToString(world, entity);

                // Entity was un-deleted; re-delete it.
                StoreEntityComponentsBeforeDestroy(entity, world, ToRememberRestore);
                world.Destroy(entity);
            }

            Logger.LogInfo($"{(!isUndoOrRedo ? "Undid" : "Redid")} {entityString}'s deletion.");

            return;
        }

        // Handle single component change case.
        Logger.LogInfo($"{(!isUndoOrRedo ? "Undid" : "Redid")} change to {EntityToString(world, entity)} for {componentPriorToChange.GetType().Name} : Reset to {componentPriorToChange.ToString()}");


        // Store current state so we can potentially 'Redo' this 'Undo' change.
        if (!WorkaroundHas(world, entity, componentPriorToChange))
        {
            var type = componentPriorToChange.GetType();
            var dummyComponent = (dynamic)Activator.CreateInstance(type);
            ToRememberRestore.Push((entity, dummyComponent, false));
        }
        else
        {
            // Component didn't exist before the change, so it's safe to assume it must exist now.
            var componentPriorToUndo = WorkaroundGet(world, entity, componentPriorToChange);
            ToRememberRestore.Push((entity, componentPriorToUndo, true));
        }

        // Undo the change.
        if (!hadComponent)
        {
            WorkaroundRemove(world, entity, componentPriorToChange);
        }
        else
        {
            WorkaroundSet(world, entity, componentPriorToChange);
        }
    }

    static void UndoLastComponentChange(World world)
    {
        UndoRedoLastComponentChange(world, ComponentChangeHistory, UndoHistory, false);
    }

    static void RedoLastComponentChange(World world)
    {
        UndoRedoLastComponentChange(world, UndoHistory, ComponentChangeHistory, true);
    }

    static void DrawHelpWindow(World world)
    {
        ImGui.Begin("Help", ImGuiWindowFlags.AlwaysAutoResize);

        var tableFlags = ImGuiTableFlags.BordersInnerV
            | ImGuiTableFlags.NoHostExtendX
            | ImGuiTableFlags.SizingFixedFit;

        if (ImGui.BeginTable("##Help", 2, tableFlags))
        {
            foreach (var (requiredInput, namedAction) in DebugKeybinds)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var key = requiredInput & ~ImGuiKey.ModMask;
                var modKey = requiredInput & ImGuiKey.ModMask;
                // Remove first 3 chars to get rid of "Mod" prefix
                var modKeyStr = modKey != 0 ? modKey.ToString().Remove(0, 3) + "+" : "";
                ImGui.Text(modKeyStr + (key != ImGuiKey.None ? key.ToString() : ""));

                ImGui.TableNextColumn();
                if (ImGui.SmallButton(namedAction.Name))
                {
                    namedAction.Invoke(world);
                }
                if (namedAction.ToggleFunc != null)
                {
                    var isChecked = !namedAction.ToggleFunc();
                    namedAction.ToggleFunc(); // toggle it again to reset it to what it was (hacky, I know).
                    ImGui.SameLine();

                    // Style manipulation is so we can shrink the checkbox; PushStyleVar would force us to change X padding too.
                    var style = ImGui.GetStyle();
                    var oldYFramePadding = style.FramePadding.Y;
                    style.FramePadding.Y = 0.0f;
                    if (ImGui.Checkbox($"##{namedAction.Name}Toggle", ref isChecked))
                    {
                        namedAction.ToggleFunc();
                    }
                    style.FramePadding.Y = oldYFramePadding;
                }
            }
            ImGui.EndTable();
        }

        ImGui.End();
    }

    static void HandleDebugKeybinds(World world)
    {
        foreach (var (key, debugAction) in DebugKeybinds)
        {
            if (ImGui.IsKeyChordPressed(key))
            {
                debugAction.Invoke(world);
            }
        }
    }

    static Dictionary<string, object> DetachedWindows = new();

    static void DrawDetachedWindows(World world)
    {
        // Credits to @APurpleApple for this trick: https://discord.com/channels/571020752904519693/571020753479401483/1347847933709783102
        foreach (var (windowTitle, obj) in DetachedWindows)
        {
            bool dontCloseWindow = true;
            ImGui.Begin(windowTitle, ref dontCloseWindow);

            if (obj.GetType() == typeof(Entity))
            {
                var entity = (Entity)obj;
                var entityComponentTypes = world.Debug_GetAllComponentTypes(entity);
                //var hasAnyComponent = false;
                foreach (var type in entityComponentTypes)
                {
                    DrawComponentInspector(world, entity, type);
                    //hasAnyComponent = true;
                }
            }
            else if (obj.GetType() == typeof(Action<World>))
            {
                var action = (Action<World>)obj;
                action(world);
            }

            ImGui.End();
            if (!dontCloseWindow)
            {
                DetachedWindows.Remove(windowTitle);
            }
        }
    }

    static HashSet<Type> ComponentTypeWindows = new();

    unsafe static ImGuiTextFilterPtr TypeSearchFilter = new(ImGuiNative.ImGuiTextFilter_ImGuiTextFilter(null));

    static void DrawComponentTypeSearch(World world)
    {
        TypeSearchFilter.Draw("Search");

        for (int i = 0; i < ComponentTypes.Count; ++i)
        {
            var type = ComponentTypes[i];

            if (TypeSearchFilter.PassFilter(type.Name))
            {
                if (ImGui.Selectable(type.Name))
                {
                    ComponentTypeWindows.Add(type);
                }
            }
        }
    }

    static void DrawEntitiesWithComponentWindows(World world)
    {
        foreach (var componentType in ComponentTypeWindows)
        {
            bool dontCloseWindow = true;
            ImGui.Begin($"Entities with {componentType.Name}", ref dontCloseWindow, ImGuiWindowFlags.AlwaysAutoResize);

            foreach (var entity in world.Debug_GetEntities(componentType))
            {
                // Don't want to spam debugger with boring/irrelevant entities.
                if (world.Has<Editor_DontShow>(entity))
                {
                    continue;
                }

                var entityStr = EntityToString(world, entity);
                bool treeIsShown = ImGui.TreeNode(entityStr);

                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    DetachedWindows.TryAdd(entityStr, entity);
                }

                if (!treeIsShown)
                {
                    continue;
                }

                foreach (var type in world.Debug_GetAllComponentTypes(entity))
                {
                    DrawComponentInspector(world, entity, type);
                }

                ImGui.TreePop();
            }

            ImGui.End();
            if (!dontCloseWindow)
            {
                ComponentTypeWindows.Remove(componentType);
            }
        }
    }

    #region Draw Components

    delegate void DrawComponentAction(World world, Entity entity, ref bool changed);

    // Credits to @cosmonaut: https://discord.com/channels/571020752904519693/591369371369209871/1298383364813881385
    static Dictionary<Type, DrawComponentAction> ComponentTypeToInspectorAction = new()
    {
        { typeof(Position2D), DrawPosition2D },
        { typeof(SpriteScale), DrawSpriteScale },
        { typeof(Direction2D), DrawDirection2D },
        { typeof(Speed), DrawSpeed },
        //{ typeof(LevelBoundaries), DrawLevelBoundariesParameters },
        //{ typeof(SpriteAnimation), DrawSpriteAnimation },
        //{ typeof(Text), DrawText },
        { typeof(Angle), DrawAngle },
        { typeof(HasHealth), DrawHealth },
        { typeof(ColorBlend), DrawColorBlend },
        { typeof(Rectangle), DrawRectangle },
        { typeof(Depth), DrawDepth },
    };

    static Dictionary<Type, Func<Entity, string>> ComponentTypeToInspectorString = new()
    {

    };


    static dynamic ComponentPriorToChange_Cached = null;

    // Credits to @cosmonaut: https://discord.com/channels/571020752904519693/591369371369209871/1298383364813881385
    private static void DrawComponentInspector(World world, Entity entity, Type type)
    {
        if (ComponentTypeToInspectorAction.ContainsKey(type))
        {
            var expanded = ImGui.CollapsingHeader(type.Name);
            if (expanded)
            {
                var dummyComponent = (dynamic)Activator.CreateInstance(type);
                var componentPriorToChange = WorkaroundGet(world, entity, dummyComponent);
                // FIXME: Destroy dummyComponent? Profile if it leaks mem.

                bool doingChanges = false;
                ComponentTypeToInspectorAction[type].Invoke(world, entity, ref doingChanges);

                // Store quick-succession changes as a single change, for the 'Undo' feature. 
                if (ComponentPriorToChange_Cached == null)
                {
                    if (doingChanges)
                    {
                        ComponentPriorToChange_Cached = componentPriorToChange;
                        ClearRedoList();
                    }
                }
                else if (!doingChanges && !ImGui.IsAnyItemActive())
                {
                    ComponentChangeHistory.Push((entity, ComponentPriorToChange_Cached, true));
                    Logger.LogInfo($"Stored prior state for {EntityToString(world, entity)}'s {ComponentPriorToChange_Cached.GetType()}: {ComponentPriorToChange_Cached}");
                    ComponentPriorToChange_Cached = null;
                }
            }
        }
        else if (ComponentTypeToInspectorString.ContainsKey(type))
        {
            ImGui.Text($"{type}: {ComponentTypeToInspectorString[type].Invoke(entity)}");
        }
        else
        {
            ImGui.Text(type.ToString());
        }
    }

    private static void DrawSpeed(World world, Entity entity, ref bool changed)
    {
        var velocity = world.Get<Speed>(entity);
        var inputVelocity = velocity.Value;

        // NOTE: Without a space or ## in this tag, we can't input anything! Weird bug.
        // Probably because the label ID is used elsewhere, but hmm.
        if (ImGui.InputFloat("##Speed", ref inputVelocity))
        {
            world.Set(entity, new Speed(inputVelocity));
            changed = true;
        }
    }

    private static void DrawRectangle(World world, Entity entity, ref bool changed)
    {
        var rect = world.Get<Rectangle>(entity);
        var inputPosOffset = new Vector2(rect.X, rect.Y);

        if (ImGui.DragFloat2("Offset", ref inputPosOffset))
        {
            world.Set(entity, new Rectangle((int)inputPosOffset.X, (int)inputPosOffset.Y, rect.Width, rect.Height));
            changed = true;
        }

        var inputSize = new Vector2(rect.Width, rect.Height);
        if (ImGui.DragFloat2("Width/Height", ref inputSize))
        {
            world.Set(entity, new Rectangle(rect.X, rect.Y, (int)inputSize.X, (int)inputSize.Y));
            changed = true;
        }
    }

    private static void DrawPosition2D(World world, Entity entity, ref bool changed)
    {
        var pos = world.Get<Position2D>(entity);
        var input = pos.AsVector();

        if (ImGui.DragFloat2("Position2D", ref input))
        {
            world.Set(entity, new Position2D(input));
            changed = true;
        }

        // Credits to @rokups for this trick: https://github.com/ocornut/imgui/discussions/3848
        // And credits to Samurai Gunn 2 behind-the-scenes vids for the idea.
        ImGui.Button("Grab");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Click and drag to adjust the position.");
            ImGui.EndTooltip();
        }
        if (ImGui.IsItemActive())
        {
            pos += ImGui.GetIO().MouseDelta;
            world.Set(entity, pos);
            changed = true;
        }
    }

    static bool UniformScaleStretch = true;

    private static void DrawSpriteScale(World world, Entity entity, ref bool changed)
    {
        var scale = world.Get<SpriteScale>(entity);

        ImGui.Checkbox("Uniform scale?", ref UniformScaleStretch);

        if (UniformScaleStretch)
        {
            var input = scale.Scale.Y;
            ImGui.Text(scale.Scale.X.ToString());
            ImGui.SameLine();
            if (ImGui.DragFloat("Scale", ref input))
            {
                var newScale = new Vector2(input, input);
                world.Set(entity, new SpriteScale(newScale));
                changed = true;
            }
        }
        else
        {
            var input = scale.Scale;
            if (ImGui.DragFloat2("Scale", ref input))
            {
                world.Set(entity, new SpriteScale(input));
                changed = true;
            }
        }
    }

    private static void DrawDirection2D(World world, Entity entity, ref bool changed)
    {
        var direction = world.Get<Direction2D>(entity);
        var input = float.RadiansToDegrees(MathUtilities.AngleFromUnitVector(direction.Value));

        if (ImGui.InputFloat("Angle (degrees)", ref input))
        {
            var output = MathUtilities.UnitVectorFromAngle(float.DegreesToRadians(input));
            world.Set(entity, new Direction2D(output));
            changed = true;
        }

        if (ImGui.SliderFloat("Slider", ref input, -360f, 360))
        {
            var output = MathUtilities.UnitVectorFromAngle(float.DegreesToRadians(input));
            world.Set(entity, new Direction2D(output));
            changed = true;
        }
    }

    private static void DrawAngle(World world, Entity entity, ref bool changed)
    {
        var angle = world.Get<Angle>(entity);
        var input = float.RadiansToDegrees(angle.Value);

        if (ImGui.InputFloat("Angle (degrees)", ref input))
        {
            var output = float.DegreesToRadians(input);
            world.Set(entity, new Angle(output));
            changed = true;
        }

        if (ImGui.SliderFloat("Slider", ref input, -360f, 360))
        {
            var output = float.DegreesToRadians(input);
            world.Set(entity, new Angle(output));
            changed = true;
        }
    }

    private static void DrawColorBlend(World world, Entity entity, ref bool changed)
    {
        var color = world.Get<ColorBlend>(entity);
        var input = color.Color.ToVector4();

        if (ImGui.ColorEdit4("Color", ref input))
        {
            var output = new Color(input);
            world.Set(entity, new ColorBlend(output));
            changed = true;
        }
    }

    private static void DrawHealth(World world, Entity entity, ref bool changed)
    {
        var health = world.Get<HasHealth>(entity);
        var input = health.Health;

        if (ImGui.InputInt("Health", ref input))
        {
            world.Set(entity, new HasHealth(input));
            changed = true;
        }
    }

    private static void DrawDepth(World world, Entity entity, ref bool changed)
    {
        var depth = world.Get<Depth>(entity);
        var input = depth.Value;

        if (ImGui.InputFloat("##Depth", ref input))
        {
            world.Set(entity, new Depth(input));
            changed = true;
        }
    }
    #endregion Draw Components

}

#endif