#if DEBUG

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Unicode;
using Hexa.NET.ImGui;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.AsyncIO;
using MoonWorks.Graphics;
using MoonWorks.Input;
using MoonWorks.Math;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Data;
using RollAndCash.Editor;
using RollAndCash.GameStates;
using RollAndCash.Relations;
using RollAndCash.Rendering;
using RollAndCash.Systems;
using RollAndCash.Utility;
using SDL3;
using Buffer = MoonWorks.Graphics.Buffer;

namespace RollAndCash.Systems;

public class EditorSystem : MoonTools.ECS.System
{
    public static List<Type> ComponentTypes = new();

    public static void StaticInit()
    {
        // FIXME: Update on hot-reload, if we add new component types?
        ReInitComponentTypesList();
    }

    MoonTools.ECS.Filter PositionFilter;

    public Entity? DebugEntity = null; // So we can stick Relations on this to safely track other entities.
    static string DebugEntityTag = "EDITOR";

    public LevelEditorManipulator LevelEditor;
    private RenderingManipulator RenderingManipulator;
    private PrefabManipulator PrefabManipulator;

    public EditorSystem(World world) : base(world)
    {
        PositionFilter =
            FilterBuilder
            .Include<Position2D>()
            .Build();

        LevelEditor = new(World, this);
        RenderingManipulator = new(World);
        PrefabManipulator = new(World);
    }

    public override void Update(TimeSpan delta)
    {
        if (!DebugEntity.HasValue)
        {
            DebugEntity = World.CreateEntity(DebugEntityTag);
            Set(DebugEntity.Value, new Editor_DontShowInLists());
            Set(DebugEntity.Value, new Editor_DontAddToLevel()); // just in case, but shouldn't be needed
            Set(DebugEntity.Value, new Editor_GlobalDebugEntity());
        }

        UpdateCachedLevelLayerEntities();

        DrawWindowMenuBar(World);
        DrawVisualSetMenus();
        EditorHelpActions.DrawHelpWindow(World);
        EditorHelpActions.HandleEditorKeybinds(World);
        DrawDetachedWindows(World);
        DrawComponents.DrawEntitiesWithComponentWindows(World);

        LevelEditor.HandleLevelEditor(DebugEntity.Value);
        HandleEntitySelectionMode();
    }

    static List<VisualSetMenu> OpenedVisualSetMenus = new();
    public void DrawVisualSetMenus()
    {
        foreach (var visualSetMenu in OpenedVisualSetMenus)
        {
            visualSetMenu.Show(World, PrefabManipulator, RenderingManipulator);
        }
    }

    static VisualSet.Editor_Types VisualSetTypeToPreviewForList = VisualSet.Editor_Types.Invalid;
    public static void DrawWindowMenuBar(World world)
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Edit"u8))
            {
                foreach (var (keybind, editorAction) in EditorHelpActions.EditorEditKeybinds)
                {
                    var isDisabled = editorAction.IsDisabled();
                    if (ImGui.MenuItem(editorAction.Name, 
                        EditorHelpActions.KeyComboToString(keybind), false, !isDisabled)
                        )
                    {
                        editorAction.Invoke(world);
                    }
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("View"u8))
            {
                if (ImGui.Selectable("TileSets"u8))
                {
                    VisualSetTypeToPreviewForList = VisualSet.Editor_Types.TileSet;
                    ImGui.OpenPopup("SelectVisualSet"u8);
                }
                else if (ImGui.Selectable("ImageSets"u8))
                {
                    VisualSetTypeToPreviewForList = VisualSet.Editor_Types.ImageSet;
                    ImGui.OpenPopup("SelectVisualSet"u8);
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginPopup("SelectVisualSet"u8))
            {
                if (VisualSetTypeToPreviewForList != VisualSet.Editor_Types.Invalid)
                {
                    var visualSets = VisualSet.Editor_VisualSetsByType[VisualSetTypeToPreviewForList];
                    foreach (var visualSet in visualSets)
                    {
                        if (ImGui.Selectable(visualSet.Name))
                        {
                            bool found = false;
                            foreach (var visualSetMenus in OpenedVisualSetMenus)
                            {
                                if (visualSetMenus.VisualSet == visualSet)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (!found)
                            {
                                OpenedVisualSetMenus.Add(new VisualSetMenu(visualSet));
                            }
                        }
                    }
                }
                else
                {
                    Logger.LogError("ERROR: Invalid visual set type??");
                    ImGui.Text("ERROR: Invalid visual set type??"u8);
                }

                ImGui.EndPopup();
            }

            ImGui.EndMainMenuBar();
        }
    }

    void UpdateCachedLevelLayerEntities()
    {
        if (LevelEditor.ActiveLevel == null)
        {
            return;
        }

        foreach (var room in LevelEditor.ActiveLevel.Rooms)
        {
            foreach (var (_, layer) in room.LayersByName)
            {
               layer.CachedEntities.Clear();
            }
        }

        foreach (var entity in PositionFilter.Entities)
        {
            var depth = Has<Depth>(entity) ? Get<Depth>(entity).Value : (float)DepthLayer.PlaceholderDepth;

            if (!Has<LevelRoomID>(entity))
            {
                continue;
            }
            var room = LevelEditor.ActiveLevel.GetRoomFromID(Get<LevelRoomID>(entity));

            LiveLevel.EditorLayer maybeLayer = null;
            if (Has<Editor_LevelLayerID>(entity))
            {
                // FIXME: might be null, if the layer has been deleted this session, then undone.
                maybeLayer = room.GetLayerFromID(Get<Editor_LevelLayerID>(entity));

                if (depth != maybeLayer.Depth)
                {
                    // Force a change of layer due to an unexpected change in depth.
                    Remove<Editor_LevelLayerID>(entity);
                    maybeLayer = null;
                }
            }

            // Automatically create a new layer to group entities that aren't in one.
            if (maybeLayer == null && !Has<Editor_DontAddToLevel>(entity))
            {
                var layerType = LevelLayerTypes.Unknown;
                string layerName;

                bool isInteger = depth == float.Floor(depth);
                if (isInteger && Enum.IsDefined((DepthLayer)(int)depth))
                {
                    layerType = LevelLayerTypes.Prefabs;
                    layerName = $"{((DepthLayer)(int)depth).ToString()}";
                }
                else
                {
                    layerName = LiveLevel.EditorLayer.LayerTypeToString(layerType);
                }

                // Try to find an existing layer to group this with.
                if (room.LayersByName.ContainsKey(layerName))
                {
                    maybeLayer = room.LayersByName[layerName];
                }
                else
                {
                    // If not, create one.
                    maybeLayer = new LiveLevel.EditorLayer(layerType, room, layerName, depth); // adds itself to lists
                }
            }

            maybeLayer.CachedEntities.Add(entity);
            Set(entity, maybeLayer.LayerID);
        }

        // TODO: Delete level layers that no longer contain any entities.
        // Maybe only those that were dynamically generated, for unrecognized depth.
    }

    public static bool IsInEntitySelectionMode = false;

    static SpatialHash<Entity> VisualEntitiesSpatialHash =
        new SpatialHash<Entity>(0, 0, Dimensions.GAME_W, Dimensions.GAME_H, 32);

    public bool CanEntityBeSelected(Entity e)
    {
        // Ignore entities that aren't in the Level Editor's currently active Room + Layer
        if (LevelEditor.ActiveLevel != null 
            && LevelEditor.ActiveRoom != null
            && LevelEditor.SelectedLayerInList != null
            )
        {
            if (Has<LevelRoomID>(e)
                && Has<Editor_LevelLayerID>(e))
            {
                var entityRoomID = Get<LevelRoomID>(e);
                if (entityRoomID != LevelEditor.ActiveRoom.ID)
                {
                    return false;
                }
                var entityLayerID = Get<Editor_LevelLayerID>(e);
                if (entityLayerID != LevelEditor.SelectedLayerInList.LayerID)
                {
                    return false;
                }
            }
            else
            {
                Logger.LogError($"WTF! Entity {EntityToString(e)} doesn't have a level layer or RoomID! Components: {EntityComponentsToString(e)}");
            }
        }
        // Allow selection mode to work, even when no levels are loaded, if we want to manually spawn entities
        // via code.
        return true;
    }

    void HandleEntitySelectionMode()
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
                    if (!CanEntityBeSelected(entity))
                    {
                        continue;
                    }

                    var worldRect = rect.Value.GetWorldRect(Get<Position2D>(entity));
                    VisualEntitiesSpatialHash.Insert(entity, worldRect);
                }
            }

            // Check what entities the mouse is hovering over.
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

            ImGui.SetTooltip($"{EntityToString(hoveredOverEntity)}");

            // Exit selection mode if we confirm our selection.
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                IsInEntitySelectionMode = false;
                Logger.LogInfo($"Selected {EntityToString(hoveredOverEntity)}");
            }

            // TODO: Switch selection to one of greater/lower depth at the same mouse position?
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
                    UndoRedo.RememberEntityDestruction(selectedEntity, World);
                    Destroy(selectedEntity);
                    UndoRedo.ClearRedoList();
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
        if (Has<Rectangle>(entity) 
            && (Has<DrawAsRectangle>(entity) || Has<HasLineHitbox>(entity)))
        {
            return Get<Rectangle>(entity);
        }
        else if (Has<SpriteAnimation>(entity))
        {
            var spriteAnim = Get<SpriteAnimation>(entity);
            var currentSprite = spriteAnim.CurrentSprite;
            var rect = currentSprite.FrameRect;
            var origin = spriteAnim.Origin;

            Vector2 scale = Vector2.One;
            if (Has<VisualScale>(entity))
            {
                scale = Get<VisualScale>(entity).Scale;
            }

            origin *= scale;

            var offset = -origin - new Vector2(currentSprite.FrameRect.X, currentSprite.FrameRect.Y) * scale;
            var visualSize = new Vector2(currentSprite.SliceRect.W, currentSprite.SliceRect.H) * scale;

            // FIXME: Account for orientation/angle!! 
            // Selection is AABB, so maybe draw an oversized rectangle to cover it all?
            var orientation = Has<Angle>(entity) ? Get<Angle>(entity).ValueInRadians : 0.0f;
            if (orientation != 0.0f)
            {
                // FIXME: Get highest & lowest points, somehow??
                // One thing is certain: the points will be at the four corners of the rectangle.
            }

            return new Rectangle(
                (int)(rect.X + offset.X),
                (int)(rect.Y + offset.Y),
                (int)visualSize.X,
                (int)visualSize.Y
            );
        }
        return null;
    }

    static void ReInitComponentTypesList()
    {
        ComponentTypes.Clear();

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
        return EntityToString(World, e);
    }

    public static string EntityComponentsToString(World world, Entity e)
    {
        string result = new("");
        foreach (var type in world.Debug_GetAllComponentTypes(e))
        {
            result += "\n*\t" + type.Name;
        }
        return result;
    }
    public string EntityComponentsToString(Entity e)
    {
        return EntityComponentsToString(World, e);
    }

    public static Dictionary<string, object> DetachedWindows = new();

    static void DrawDetachedWindows(World world)
    {
        // Credits to @APurpleApple for this trick: https://discord.com/channels/571020752904519693/571020753479401483/1347847933709783102
        foreach (var (windowTitle, obj) in DetachedWindows)
        {
            bool dontCloseWindow = true;
            if (ImGui.Begin(windowTitle, ref dontCloseWindow))
            {
                if (obj.GetType() == typeof(Entity))
                {
                    var entity = (Entity)obj;
                    var entityComponentTypes = world.Debug_GetAllComponentTypes(entity);
                    //var hasAnyComponent = false;
                    foreach (var type in entityComponentTypes)
                    {
                        DrawComponents.DrawComponentInspector(world, entity, type);
                        //hasAnyComponent = true;
                    }
                }
                else if (obj.GetType() == typeof(Action<World>))
                {
                    var action = (Action<World>)obj;
                    action(world);
                }
            }
            ImGui.End();

            if (!dontCloseWindow)
            {
                DetachedWindows.Remove(windowTitle);
            }
        }
    }
}

#endif