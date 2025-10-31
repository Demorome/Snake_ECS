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

    public EditorSystem(World world) : base(world)
    {
        PositionFilter =
            FilterBuilder
            .Include<Position2D>()
            .Build();

        LevelEditor = new(World, this);
    }

    public override void Update(TimeSpan delta)
    {
        if (!DebugEntity.HasValue)
        {
            DebugEntity = World.CreateEntity(DebugEntityTag);
            Set(DebugEntity.Value, new Editor_DontShowInLists());
            Set(DebugEntity.Value, new Editor_DebugEntity());
        }

        UpdateCachedLevelLayerEntities();

        EditorHelpActions.DrawWindowMenuBar(World);
        EditorHelpActions.DrawHelpWindow(World);
        EditorHelpActions.HandleEditorKeybinds(World);
        DrawDetachedWindows(World);
        DrawComponents.DrawEntitiesWithComponentWindows(World);

        LevelEditor.HandleLevelEditor(DebugEntity.Value);
        HandleEntitySelectionMode();
    }

    void UpdateCachedLevelLayerEntities()
    {
        foreach (var (_, levelLayer) in LevelEditor.Level.Layers)
        {
            levelLayer.CachedEntities.Clear();
        }

        foreach (var entity in PositionFilter.Entities)
        {
            var depth = Has<Depth>(entity) ? Get<Depth>(entity).Value : (float)DepthLayer.DefaultDepth;

            Level.Layer maybeLayer = null;
            if (Has<Editor_LevelLayerID>(entity))
            {
                // FIXME: might be null, if the layer has been deleted this session, then undone.
                maybeLayer = LevelEditor.Level.GetLayerFromID(Get<Editor_LevelLayerID>(entity));

                if (depth != maybeLayer.Depth)
                {
                    // Force a change of layer.
                    Remove<Editor_LevelLayerID>(entity);
                    maybeLayer = null;
                }
            }

            if (maybeLayer == null)
            {
                var layerType = Level.Layer.Types.Unknown;
                string layerName;

                bool isInteger = depth == float.Floor(depth);
                if (isInteger && Enum.IsDefined((DepthLayer)(int)depth))
                {
                    layerType = Level.Layer.Types.Prefab;
                    layerName = $"{((DepthLayer)(int)depth).ToString()}";
                }
                else
                {
                    layerName = Level.Layer.LayerTypeToString(layerType);
                }

                // Try to find an existing layer to group this with, based on depth.
                if (LevelEditor.Level.Layers.ContainsKey(layerName))
                {
                    maybeLayer = LevelEditor.Level.Layers[layerName];
                }
                else
                {
                    // If not, create one.
                    maybeLayer = new Level.Layer(layerType, LevelEditor.Level, layerName, depth);
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
                    // Ignore entities that aren't in the Level Editor's currently active Editor Layer
                    if (LevelEditor.SelectedLayerName != null)
                    {
                        if (Has<Editor_LevelLayerID>(entity))
                        {
                            var selectedLayer = LevelEditor.Level.Layers[LevelEditor.SelectedLayerName];
                            var entityLayerID = Get<Editor_LevelLayerID>(entity);
                            if (entityLayerID != selectedLayer.LayerID)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"WTF! Visual entity {entity.ID} doesn't have a level layer! Components:");
                            foreach (var type in World.Debug_GetAllComponentTypes(entity))
                            {
                                Console.WriteLine("*\t" + type.Name);
                            }
                        }

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
                    Logger.LogInfo($"Deleted {EntityToString(selectedEntity)}");
                    UndoRedo.StoreEntityDestroyHistory(selectedEntity, World);
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
            if (Has<SpriteScale>(entity))
            {
                scale = Get<SpriteScale>(entity).Scale;
            }

            origin *= scale;

            var offset = -origin - new Vector2(currentSprite.FrameRect.X, currentSprite.FrameRect.Y) * scale;
            var visualSize = new Vector2(currentSprite.SliceRect.W, currentSprite.SliceRect.H) * scale;

            // FIXME: Account for orientation/angle!! 
            // Selection is AABB, so maybe draw an oversized rectangle to cover it all?
            var orientation = Has<Angle>(entity) ? Get<Angle>(entity).Value : 0.0f;
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
        var tag = World.GetTag(e);
        if (tag.Length == 0)
        {
            return e.ToString();
        }
        return $"Entity {{ ID = {e.ID}, Tag = {tag} }}";
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