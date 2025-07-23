#if DEBUG

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Unicode;
using ImGuiNET;
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
        InitComponentTypesList();
    }

    TileManipulator TileManipulator;

    MoonTools.ECS.Filter PositionFilter, LevelLayerFilter;

    public Entity? DebugEntity = null; // So we can stick Relations on this to safely track other entities.
    static string DebugEntityTag = "EDITOR";

    public EditorSystem(World world) : base(world)
    {
        PositionFilter = FilterBuilder.Include<Position2D>().Build();
        LevelLayerFilter = FilterBuilder.Include<Editor_LevelLayerID>().Build();

        TileManipulator = new(world);
    }

    public override void Update(TimeSpan delta)
    {
        if (!DebugEntity.HasValue)
        {
            DebugEntity = World.CreateEntity(DebugEntityTag);
            Set(DebugEntity.Value, new Editor_DontShowInLists());
        }

        foreach (var levelLayer in LevelLayers)
        {
            levelLayer.CachedEntities.Clear();
        }

        foreach (var entity in LevelLayerFilter.Entities)
        {
            var layerID = Get<Editor_LevelLayerID>(entity);
            LevelLayers[layerID.Value].CachedEntities.Add(entity);
        }

        EditorHelpActions.DrawWindowMenuBar(World);
        EditorHelpActions.DrawHelpWindow(World);
        EditorHelpActions.HandleEditorKeybinds(World);
        DrawDetachedWindows(World);
        DrawComponents.DrawEntitiesWithComponentWindows(World);

        HandleSelectionMode();
        HandleLevelEditor();
	}

    public static void ShowPrefabSpawnerWindow(World world)
    {
        /*if (ImGui.Button())
        {

        }*/
        // TODO: Once button to spawn a prefab entity is pressed, make it appear transparent below cursor.
        // TODO: Pressing click will spawn it.
        // TODO: If spawned, add to change history.
    }

    List<LevelLayer> LevelLayers = new(); // FIXME: Load from level data

    int ActiveLayerID = -1;
    int SelectedLayerID = -1;
    public bool IsActiveLayerTiled => LevelLayers[ActiveLayerID].IsTiled;
    public float ActiveLayerDepth => LevelLayers[ActiveLayerID].Depth;

    const string TileSpritePrefix = "Tile_";
    static int TileSpriteToReplaceIndex = -1;
    static int SelectedTileSpriteIndex = -1;
    public SpriteAnimationInfo GetSelectedSpriteToPaint()
    {
        if (SelectedTileSpriteIndex == -1 || ActiveLayerID == -1)
        {
            return null;
        }
        return SpriteAnimationInfo.FromID(LevelLayers[ActiveLayerID].Images[SelectedTileSpriteIndex].Item1);
    }
    public Color GetSelectedSpriteColor()
    {
        var tileSetColor = LevelLayers[ActiveLayerID].ColorBlend;
        var tileColor = LevelLayers[ActiveLayerID].Images[SelectedTileSpriteIndex].Item2;
        return Color.Lerp(tileSetColor, tileColor, 0.5f);
    }

    void OnLayerVisibilityChange(LevelLayer layer)
    {
        if (!layer.IsVisible)
        {
            // Hide every entity in this layer
            foreach (var entity in layer.CachedEntities)
            {
                Relate(entity, DebugEntity.Value, new DontDraw());
            }
        }
        else
        {
            // Un-hide every entity in this layer
            foreach (var entity in layer.CachedEntities)
            {
                Unrelate<DontDraw>(entity, DebugEntity.Value);
            }
        }
    }

    unsafe static ImGuiTextFilterPtr TileSpriteSearchFilter = new(ImGuiNative.ImGuiTextFilter_ImGuiTextFilter(null));

    static void DrawTileSpriteReplacementsPopup(LevelLayer levelLayer)
    {
        if (ImGui.BeginPopup("##SelectTileSprite"))
        {
            ImGui.Text("Select Tile Sprite Replacement");
            ImGui.Separator();
            TileSpriteSearchFilter.Draw("Search");

            foreach (var spriteName in SpriteAnimations.Names)
            {
                if (!spriteName.StartsWith(TileSpritePrefix))
                {
                    continue;
                }

                if (TileSpriteSearchFilter.PassFilter(spriteName))
                {
                    if (ImGui.Selectable(spriteName))
                    {
                        var spriteID = SpriteAnimations.NameToInfoMap[spriteName].ID;
                        levelLayer.Images[TileSpriteToReplaceIndex] = (spriteID, Color.White);
                        TileSpriteToReplaceIndex = -1;
                        break;
                    }
                }
            }
        }
        else
        {
            TileSpriteToReplaceIndex = -1;
        }
        ImGui.EndPopup();
    }

    void ShowTileLayerMenu(LevelLayer tileLayer)
    {
        // TODO: Color blend default override option for a specific sprite in the tileset.

        // TODO: Color blend default override for the entire tileset.

        // TODO: Changing color blend overrides applies it to already placed world tiles.

        var imageBgColor = Color.Transparent;
        var layerColorBlend = tileLayer.ColorBlend;

        var scalingFactor = ImGui.GetWindowViewport().Size / Dimensions.GAME_DIMENSIONS;
        var tileSizeScaled = Dimensions.TILE_DIMENSIONS * scalingFactor;
        
        // Draw with 1 pixel gaps between sprites.
        // Helpful explanation: https://github.com/ocornut/imgui/issues/4216#issuecomment-860007592
        // FIXME: How to have gray outline but not make the background for the image gray??
        ImGui.PushStyleColor(ImGuiCol.Button, Color.Gray.ToVector4());
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2.0f, 2.0f));
        //ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(1.0f, 1.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, 0f));

        for (int i = 0; i < tileLayer.Images.Count; ++i)
        {
            var (spriteID, colorBlend) = tileLayer.Images[i];

            SpriteAnimationInfo animInfo = spriteID.ID != -1 ?
                SpriteAnimationInfo.FromID(spriteID)
                : SpriteAnimations.EditorTile_InvalidTile;

            bool invalid = animInfo.ID == SpriteAnimations.EditorTile_InvalidTile.ID;

            // FIXME: Allow sprite animations to play (simulate frame countdown?)
            var currentFrame = animInfo.Frames[0];

            bool selected = GetSelectedSpriteToPaint() != null;

            if (i == TileSpriteToReplaceIndex)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Color.Red.ToVector4());
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Color.Red.ToVector4());
            }
            else if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Color.Purple.ToVector4());
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Color.Purple.ToVector4());
            }

            if (ImGuiExtensions.ImageButton(
                i.ToString(),
                currentFrame.Texture,
                tileSizeScaled,
                currentFrame.UV.LeftTop,
                currentFrame.UV.RightBottom,
                invalid ? Color.White.ToVector4() : imageBgColor.ToVector4(),
                Color.Lerp(layerColorBlend, colorBlend, 0.5f).ToVector4(),
                ImGuiBackend.SamplerType.PointClamp
                ))
            {
                if (invalid)
                {
                    TileSpriteToReplaceIndex = i;
                }
                else
                {
                    if (selected)
                    {
                        SelectedTileSpriteIndex = -1;
                    }
                    else
                    {
                        SelectedTileSpriteIndex = i;
                    }
                }
            }

            if (selected || i == TileSpriteToReplaceIndex)
            {
                ImGui.PopStyleColor(2);
            }

            // TODO: Right-clicking on a sprite opens a menu to replace the sprite for any other "Tile"-named sprite
            // TODO: Highlight this sprite tile as green when selected this way.
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup("##SelectTileSprite");
                TileSpriteToReplaceIndex = i;
            }

            if (i == 0 || (i % (tileLayer.ImagesPerRow - 1)) != 0)
            {
                ImGui.SameLine();
            }
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();

        ImGui.NewLine();
        ImGui.Separator();

        // Draw a "[+]" square image that, if pressed, adds a new sprite slot for the tileset.
        var plusSprite = SpriteAnimations.EditorTile_Plus.Frames[0];
        if (ImGuiExtensions.ImageButton(
            "##Plus",
            plusSprite.Texture,
            plusSprite.SliceSize * scalingFactor,
            plusSprite.UV.LeftTop,
            plusSprite.UV.RightBottom,
            imageBgColor.ToVector4(),
            ImGuiBackend.SamplerType.PointClamp
            ))
        {
            tileLayer.Images.Add((new SpriteAnimationInfoID(-1), Color.White));
        }
        ImGui.SameLine();
        ImGui.TextWrapped("Add new tiles");

        var colorBlendVec = layerColorBlend.ToVector4();
        if (ImGui.ColorEdit4("Layer Color Blend", ref colorBlendVec))
        {
            tileLayer.ColorBlend = new Color(colorBlendVec);
        }

        var tileColor = Color.White.ToVector4();
        if (SelectedTileSpriteIndex == -1)
        {
            ImGui.BeginDisabled();
        }
        else
        {
            tileColor = tileLayer.Images[SelectedTileSpriteIndex].Item2.ToVector4();
        }
        if (ImGui.ColorEdit4("Tile Color Blend", ref tileColor))
        {
            var (spriteID, color) = tileLayer.Images[SelectedTileSpriteIndex];
            tileLayer.Images[SelectedTileSpriteIndex] = (spriteID, new Color(tileColor));
        }
        if (SelectedTileSpriteIndex == -1)
        {
            ImGui.EndDisabled();
        }

        ImGui.InputInt("Tiles per row", ref tileLayer.ImagesPerRow);

        if (TileSpriteToReplaceIndex != -1)
        {
            DrawTileSpriteReplacementsPopup(tileLayer);
        }
    }

    void ShowLevelLayerOptions()
    {
        if (ImGui.Begin("Level Layers"))
        {
            for (int i = 0; i < LevelLayers.Count; ++i)
            {
                var layer = LevelLayers[i];
                if (ImGui.Checkbox("##" + layer.Name + "Visibility", ref layer.IsVisible))
                {
                    OnLayerVisibilityChange(layer);
                }
                ImGui.SameLine();

                if (ImGui.Selectable(layer.Name, SelectedLayerID == i, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    SelectedLayerID = i;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        ActiveLayerID = i;
                    }
                }

                if (ImGui.BeginPopup($"RenameLayer{i}"))
                {
                    string newName = layer.Name;
                    for (int c = newName.Length - 1; c >= 0; --c)
                    {
                        if (char.IsAsciiDigit(newName[c]) || char.IsWhiteSpace(newName[c]))
                        {
                            newName = newName.Remove(c, 1);
                        }
                        else
                        {
                            break;
                        }
                    }
                    // FIXME: Would be nice if we could prevent numbers from being input here...
                    if (ImGui.InputText("##RenameLayerText", ref newName, 100, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        LevelLayer.LevelLayerNames.Remove(layer.Name);
                        LevelLayer.ValidateLayerName(ref newName);
                        layer.Name = newName;
                    }
                    ImGui.EndPopup();
                }
            }

            if (ImGui.Button("New"))
            {
                ImGui.OpenPopup("ChooseLayerType");
            }
            if (ImGui.BeginPopup("ChooseLayerType"))
            {
                ImGui.SeparatorText("Layer Type");
                for (int i = 0; i < (int)LevelLayer.LevelLayerTypes.COUNT; ++i)
                {
                    var layerType = (LevelLayer.LevelLayerTypes)i;
                    var layerTypeStr = LevelLayer.LayerTypeToString(layerType);
                    if (ImGui.Selectable(layerTypeStr))
                    {
                        LevelLayers.Add(new LevelLayer(layerType, layerTypeStr + " Layer"));
                    }
                }
                ImGui.EndPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("Delete") && SelectedLayerID != -1)
            {
                LevelLayers.RemoveAt(SelectedLayerID);
                SelectedLayerID = -1;
            }

            ImGui.SameLine();
            if (ImGui.Button("Rename") && SelectedLayerID != -1)
            {
                ImGui.OpenPopup($"RenameLayer{SelectedLayerID}");
            }
        }
        ImGui.End();


        // Draw separate window to show the active layer options.
        if (ActiveLayerID != -1)
        {
            var layer = LevelLayers[ActiveLayerID];

            bool stayOpen = true;
            if (ImGui.Begin(layer.Name, ref stayOpen))
            {
                switch (layer.LayerType)
                {
                    case LevelLayer.LevelLayerTypes.SolidTile:
                    case LevelLayer.LevelLayerTypes.VisualTile:
                        ShowTileLayerMenu(layer);
                        break;
                    default:
                        // TODO: 
                        break;
                }
            }
            ImGui.End();
            if (!stayOpen)
            {
                ActiveLayerID = -1;
            }
        }
    }

    public static bool IsInLevelEditor = false;
    static Dictionary<string, Action<World>> LevelEditorDetachedWindows = new();
    static bool SnapToGrid = true;
    public static bool ShowGrid = true;
    public Vector2? HoveredOverTilePosition = null;
    public static Vector4 GridLineColor = (Color.DarkTurquoise * 0.5f).ToVector4();

    // Layout inspired by Elias Daler's tutorial series: https://edw.is/using-imgui-with-sfml-pt1/
    void DrawLevelEditorMainWindow()
    {
        bool stillOpened = IsInLevelEditor;
        if (ImGui.Begin("Level Editor", ref stillOpened))
        {
            //FIXME: ImGui.Text("Level path: ");
            //FIXME: ImGui.Text("Camera: ");
            ImGui.Text($"Mouse world position: {Input.WorldMousePosition}");
            ImGui.Text($"Tile position: {TileManipulator.GetTilePos(Input.WorldMousePosition)}");

            // TODO: Snap to grid option? Not sure if I should support going off-grid yet.

            ImGui.Checkbox("Show Grid?", ref ShowGrid);
            ImGui.ColorEdit4("Grid Line Color", ref GridLineColor);
        }
        ImGui.End();

        if (!stillOpened)
        {
            IsInLevelEditor = false;
        }
    }

    void HandleLevelEditor()
    {
        if (!IsInLevelEditor)
        {
            return;
        }

        DrawLevelEditorMainWindow();
        ShowLevelLayerOptions();

        /*foreach (var (windowTitle, drawAction) in LevelEditorDetachedWindows)
        {
            bool dontCloseWindow = true;
            if (ImGui.Begin(windowTitle, ref dontCloseWindow))
            {
                drawAction(World);
                ImGui.End();
            }
            if (!dontCloseWindow)
            {
                LevelEditorDetachedWindows.Remove(windowTitle);
            }
        }*/
        
        // Layer painting controls.
        var mouseHoveringOverAnyWindow = ImGui.GetIO().WantCaptureMouse;
        if (mouseHoveringOverAnyWindow)
        {
            return;
        }
        var mouseWorldPos = Input.WorldMousePosition;
        HoveredOverTilePosition = TileManipulator.GetTilePos(mouseWorldPos);
        if (!HoveredOverTilePosition.HasValue)
        {
            return;
        }
        if (ActiveLayerID == -1)
        {
            return;
        }
        var activeLayer = LevelLayers[ActiveLayerID];
        var selectedSprite = GetSelectedSpriteToPaint();
        if (selectedSprite != null && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            Entity? paintedEntity = null;

            // Painting sprites to the world!
            if (activeLayer.IsTiled)
            {
                var tileWorldPos = TileManipulator.TilePosToWorldPos_Centered(HoveredOverTilePosition.Value);

                // Don't spawn anything if tile is already painted in at this level layer.
                bool spawn = true;
                foreach (var entity in activeLayer.CachedEntities)
                {
                    if (Get<Position2D>(entity) == tileWorldPos)
                    {
                        spawn = false;
                        break;
                    }
                }

                if (spawn)
                {
                    if (activeLayer.LayerType == LevelLayer.LevelLayerTypes.SolidTile)
                    {
                        paintedEntity = TileManipulator.SpawnSolidTile(tileWorldPos, new SpriteAnimation(selectedSprite));
                    }
                    else
                    {
                        paintedEntity = CreateEntity("Visual Tile");
                        Set(paintedEntity.Value, tileWorldPos);
                        Set(paintedEntity.Value, new SpriteAnimation(selectedSprite));
                    }

                    if (SelectedTileSpriteIndex < 0)
                    {
                        throw new Exception("Tile sprite index should be valid here!");
                    }
                    Set(paintedEntity.Value, new Editor_TileSpriteIndex(SelectedTileSpriteIndex));
                }
            }
            else if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                // Assume it's an image layer.
                paintedEntity = CreateEntity("Image");
                Set(paintedEntity.Value, mouseWorldPos);
                Set(paintedEntity.Value, new SpriteAnimation(selectedSprite));
            }

            if (paintedEntity.HasValue)
            {
                Set(paintedEntity.Value, new Editor_LevelLayerID(ActiveLayerID));

                if (activeLayer.LayerType != LevelLayer.LevelLayerTypes.SolidTile)
                {
                    // FIXME: Account for depth from ActiveLayer
                }

                // FIXME: Account for color blends from ActiveLayer + selected tile blend override

                // FIXME: Group together multiple entities created in a single paintbrush stroke for Undo.
                UndoRedo.StoreEntityCreateHistory(paintedEntity.Value, World);

                //Set(paintedEntity, new Depth());
                //Set(paintedEntity, new ColorBlend());
            }
        }
        else if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            // Delete tiles on this level layer!
            if (activeLayer.IsTiled || ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                var mouseHitboxRect = new Rectangle(0, 0, 1, 1);
                var mouseWorldPosRect = mouseHitboxRect.GetWorldRect(mouseWorldPos);

                // Hopefully won't need an acceleration structure for this...
                foreach (var entity in activeLayer.CachedEntities)
                {
                    var rect = GetEntityVisualRect(entity);
                    var worldRect = rect.Value.GetWorldRect(Get<Position2D>(entity));

                    if (worldRect.Intersects(mouseWorldPosRect))
                    {
                        // FIXME: Group together deletions done while holding the mouse down
                        UndoRedo.StoreEntityDestroyHistory(entity, World);
                        Destroy(entity);
                    }
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

            ImGui.SetTooltip($"{EntityToString(hoveredOverEntity)}");

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

                ImGui.End();
            }

            if (!dontCloseWindow)
            {
                DetachedWindows.Remove(windowTitle);
            }
        }
    }
}

#endif