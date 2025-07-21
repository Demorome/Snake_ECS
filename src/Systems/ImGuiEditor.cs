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

    MoonTools.ECS.Filter PositionFilter, LevelLayerFilter;

    public Entity? DebugEntity = null; // So we can stick Relations on this to safely track other entities.
    static string DebugEntityTag = "EDITOR";

    public ImGuiEditor(World world) : base(world)
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

        DrawWindowMenuBar(World);
        DrawHelpWindow(World);
        HandleEditorKeybinds(World);
        DrawDetachedWindows(World);
        DrawEntitiesWithComponentWindows(World);

        HandleSelectionMode();
        HandleLevelEditor();
	}

    class EditorAction
    {
        public EditorAction(string name, Action<World> action,
            bool opensWindow = false, Func<bool> isDisabledFunc = null)
        {
            WorldAction = action;
            Name = name;
            OpensWindow = opensWindow;
            IsDisabledFunc = isDisabledFunc;
        }
        public EditorAction(string name, Func<bool> func)
        {
            ToggleFunc = func;
            Name = name;
        }

        public string Name;
        public Action<World> WorldAction = null;
        public Func<bool> ToggleFunc = null;
        public Func<bool> IsDisabledFunc = null;
        public bool OpensWindow = false;
        public bool ShowInEditWindow = false;

        public bool IsDisabled()
        {
            if (IsDisabledFunc != null && IsDisabledFunc())
            {
                return true;
            }
            return false;
        }

        public bool? Invoke(World world)
        {
            if (IsDisabled())
            {
                return false;
            }

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

    static Dictionary<ImGuiKey, EditorAction> EditorHelpKeybinds = new()
    {
        { ImGuiKey.F1,                   new("Search By Component", DrawComponentTypeSearch, true)},
        { ImGuiKey.ModCtrl | ImGuiKey.T, new("Show Colliders",
            () => { return Renderer.DrawDebugColliders = !Renderer.DrawDebugColliders; } )
        },
        { ImGuiKey.F6,                   new("Toggle Freeze All",
            () => { return GameplayState.FreezeTimeForAll = !GameplayState.FreezeTimeForAll; } )
        },
        { ImGuiKey.MouseX2,              new("Toggle Selection Mode",
             () => { return IsInEntitySelectionMode = !IsInEntitySelectionMode; } )
        },
        { ImGuiKey.None,                 new("Toggle Level Editor",
            () => { return IsInLevelEditor = !IsInLevelEditor; } )
            // FIXME: Once had a startup where ImGui was unresponsive and this was flickering back and forth (undefined behavior somewhere??)
        },
        { ImGuiKey.F2,                   new("Prefabs", ShowPrefabSpawnerWindow, true )},
    };
    
    static Dictionary<ImGuiKey, EditorAction> EditorEditKeybinds = new()
    {
        { ImGuiKey.ModCtrl | ImGuiKey.Z, new("Undo", UndoLastComponentChange, false, () => ChangeHistory.Count == 0) },
        { ImGuiKey.ModCtrl | ImGuiKey.Y, new("Redo", RedoLastComponentChange, false, () => UndoHistory.Count == 0) },
    };

    static void DrawWindowMenuBar(World world)
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Edit"))
            {
                foreach (var (keybind, editorAction) in EditorEditKeybinds)
                {
                    var isDisabled = editorAction.IsDisabled();
                    if (ImGui.MenuItem(editorAction.Name, KeyComboToString(keybind), false, !isDisabled))
                    {
                        editorAction.Invoke(world);
                    }
                }
                
                /*
                ImGui.Separator();
                if (ImGui::MenuItem("Cut", "CTRL+X")) { }
                if (ImGui::MenuItem("Copy", "CTRL+C")) {}
                if (ImGui::MenuItem("Paste", "CTRL+V")) {}*/
                ImGui.EndMenu();
            }
            ImGui.EndMainMenuBar();
        }
    }

    static void ShowPrefabSpawnerWindow(World world)
    {
        /*if (ImGui.Button())
        {

        }*/
        // TODO: Once button to spawn a prefab entity is pressed, make it appear transparent below cursor.
        // TODO: Pressing click will spawn it.
        // TODO: If spawned, add to change history.
    }

    const int TileSpriteColumnCount = 10;
    const string TileSpritePrefix = "Tile_";

    // FIXME: Detect if a tile sprite is shared amongst different layers and report an error.
    public static SpriteAnimationInfo SelectedSpriteToPaint = null;
    static int SelectedTileSpriteIndex = -1; 
    static bool ReplacingTileSprite = false; 
    static int TileSpriteToReplaceIndex = -1;

    class LevelLayer
    {
        public static HashSet<string> LevelLayerNames = new();

        public static void ValidateLayerName(ref string name)
        {
            name += " ";
            int i = 1;
            var testName = name + i.ToString();

            lock (LevelLayerNames)
            {
                while (LevelLayerNames.Contains(testName))
                {
                    ++i;
                    testName = name + i.ToString();
                }
                LevelLayerNames.Add(testName);
            }
            name = testName;
        }

        public enum LevelLayerTypes
        {
            Image = 0,
            VisualTile,
            SolidTile,
            COUNT
        }
        public static string LayerTypeToString(LevelLayerTypes layerType)
        {
            return layerType switch 
            {
                LevelLayerTypes.Image => "Image",
                LevelLayerTypes.VisualTile => "Visual Tile",
                LevelLayerTypes.SolidTile => "Solid Tile",
                _ => "Invalid level layer type"
            };
        }

        public LevelLayer(LevelLayerTypes layerType, string name = "New Layer")
        {
            LayerType = layerType;
            ValidateLayerName(ref name);
            Name = name;
        }

        public string Name;
        public LevelLayerTypes LayerType { get; private set; }
        public bool IsTiled => LayerType == LevelLayerTypes.VisualTile || LayerType == LevelLayerTypes.SolidTile;
        // Applies to all images.
        public Color ColorBlend = Color.White;
        public List<(SpriteAnimationInfoID, Color)> Images = new();
        public float Depth = -9999;
        public bool IsVisible = true;
        public List<Entity> CachedEntities = new();
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

    static void DrawTileSpriteReplacementsWindow(LevelLayer levelLayer)
    {
        if (!ReplacingTileSprite || !ImGui.Begin("Select Tile Sprite", ref ReplacingTileSprite))
        {
            return;
        }

        TileSpriteSearchFilter.Draw("Search");

        foreach (var spriteName in SpriteAnimations.Names)
        {
            if (!spriteName.StartsWith(TileSpritePrefix))
            {
                continue;
            }

            if (TypeSearchFilter.PassFilter(spriteName))
            {
                if (ImGui.Selectable(spriteName))
                {
                    var spriteID = SpriteAnimations.NameToInfoMap[spriteName].ID;
                    levelLayer.Images[TileSpriteToReplaceIndex] = (spriteID, Color.White);
                    TileSpriteToReplaceIndex = -1;
                    ReplacingTileSprite = false;
                }
            }
        }

        ImGui.End();
    }

    static void ShowTileLayerMenu(LevelLayer tileLayer)
    {
        // TODO: Color blend default override option for a specific sprite in the tileset.

        // TODO: Color blend default override for the entire tileset.

        // TODO: Changing color blend overrides applies it to already placed world tiles.

        var imageBgColor = Color.Transparent;
        var scalingFactor = ImGui.GetWindowViewport().Size / Dimensions.GAME_DIMENSIONS;

        // Draw with 1 pixel gaps between sprites.
        // Helpful explanation: https://github.com/ocornut/imgui/issues/4216#issuecomment-860007592
        // FIXME: How to have gray outline but not make the background for the image gray??
        ImGui.PushStyleColor(ImGuiCol.Button, Color.Gray.ToVector4());
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(1.0f, 1.0f));
        //ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(1.0f, 1.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, 0f));

        var col = 0;
        for (int i = 0; i < tileLayer.Images.Count; ++i)
        {
            var (spriteID, colorBlend) = tileLayer.Images[i];

            SpriteAnimationInfo animInfo = spriteID.ID != -1 ?
                SpriteAnimationInfo.FromID(spriteID)
                : SpriteAnimations.EditorTile_InvalidTile;

            if (animInfo.ID != SpriteAnimations.EditorTile_InvalidTile.ID)
            {
                // FIXME: Allow sprite animations to play (simulate frame countdown?)
                var currentFrame = animInfo.Frames[0];

                bool wasSelected = SelectedSpriteToPaint != null
                    && SelectedSpriteToPaint.ID == animInfo.ID
                    && SelectedTileSpriteIndex == i;

                if (wasSelected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, Color.Green.ToVector4());
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Color.Green.ToVector4());
                }
                else if (ReplacingTileSprite)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, Color.Red.ToVector4());
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Color.Red.ToVector4());
                }

                if (ImGuiExtensions.ImageButton(
                    i.ToString(),
                    currentFrame.Texture,
                    currentFrame.SliceSize * scalingFactor,
                    currentFrame.UV.LeftTop,
                    currentFrame.UV.RightBottom,
                    imageBgColor.ToVector4(),
                    ImGuiBackend.SamplerType.PointClamp
                    ))
                {
                    ReplacingTileSprite = false;
                    if (wasSelected)
                    {
                        SelectedSpriteToPaint = null;
                        SelectedTileSpriteIndex = -1;
                    }
                    else
                    {
                        SelectedSpriteToPaint = animInfo;
                        SelectedTileSpriteIndex = i;
                    }
                }

                if (wasSelected || ReplacingTileSprite)
                {
                    ImGui.PopStyleColor(2);
                }
            }
            else
            {
                // FIXME: Make this not a button. Somehow, that makes this not show up??
                var sprite = SpriteAnimations.EditorTile_InvalidTile.Frames[0];
                ImGuiExtensions.ImageButton(
                     i.ToString(),
                    sprite.Texture,
                    sprite.SliceSize * scalingFactor,
                    sprite.UV.LeftTop,
                    sprite.UV.RightBottom,
                    imageBgColor.ToVector4(),
                    colorBlend.ToVector4(),
                    ImGuiBackend.SamplerType.PointClamp
                );
            }

            // TODO: Right-clicking on a sprite opens a menu to replace the sprite for any other "Tile"-named sprite
            // TODO: Highlight this sprite tile as green when selected this way.
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                ReplacingTileSprite = true;
                TileSpriteToReplaceIndex = i;
            }

            ++col;
            col %= TileSpriteColumnCount;
            if (col != 0)
            {
                //ImGui.SameLine();
            }
        }

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

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();

        DrawTileSpriteReplacementsWindow(tileLayer);
    }

    List<LevelLayer> LevelLayers = new(); // FIXME: Load from level data
    int ActiveLayerID = -1;
    int SelectedLayerID = -1;
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
                        LevelLayers.Add(new LevelLayer(layerType, layerTypeStr + " Layer")
                        );
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
        if (SelectedSpriteToPaint != null && ImGui.IsMouseDown(ImGuiMouseButton.Left))
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
                        paintedEntity = TileManipulator.SpawnSolidTile(tileWorldPos, new SpriteAnimation(SelectedSpriteToPaint));
                    }
                    else
                    {
                        paintedEntity = CreateEntity("Visual Tile");
                        Set(paintedEntity.Value, tileWorldPos);
                        Set(paintedEntity.Value, new SpriteAnimation(SelectedSpriteToPaint));
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
                Set(paintedEntity.Value, new SpriteAnimation(SelectedSpriteToPaint));
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
                StoreEntityCreateHistory(paintedEntity.Value, World);

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
                        StoreEntityDestroyHistory(entity, World);
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
                    StoreEntityDestroyHistory(selectedEntity, World);
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
    static Stack<(Entity, dynamic, bool)> ChangeHistory = new();
    // For Ctrl+Y 'Redo' feature.
    static Stack<(Entity, dynamic, bool)> UndoHistory = new();

    static void StoreEntityDestroyHistory(Entity entity, World world)
    {
        StoreEntityComponents(entity, world, ChangeHistory, true);
    }
    static void StoreEntityCreateHistory(Entity entity, World world)
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
                StoreEntityComponents(entity, world, ToRememberRestore, true);
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
        UndoRedoLastComponentChange(world, ChangeHistory, UndoHistory, false);
    }

    static void RedoLastComponentChange(World world)
    {
        UndoRedoLastComponentChange(world, UndoHistory, ChangeHistory, true);
    }

    static string KeyComboToString(ImGuiKey keyChordCombo)
    {
        var key = keyChordCombo & ~ImGuiKey.ModMask;
        var modKey = keyChordCombo & ImGuiKey.ModMask;
        // Remove first 3 chars to get rid of "Mod" prefix
        var modKeyStr = modKey != 0 ? modKey.ToString().Remove(0, 3) + "+" : "";

        return modKeyStr + (key != ImGuiKey.None ? key.ToString() : "");
    }

    static void DrawHelpWindow(World world)
    {
        ImGui.Begin("Help", ImGuiWindowFlags.AlwaysAutoResize);

        var tableFlags = ImGuiTableFlags.BordersInnerV
            | ImGuiTableFlags.NoHostExtendX
            | ImGuiTableFlags.SizingFixedFit;

        if (ImGui.BeginTable("##Help_Table", 2, tableFlags))
        {
            foreach (var (requiredInput, namedAction) in EditorHelpKeybinds)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();

                ImGui.Text(KeyComboToString(requiredInput));

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

                    // Style manipulation is so we can shrink the checkbox; 
                    // PushStyleVar would force us to change X padding too.
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

    static void HandleEditorKeybinds(World world)
    {
        foreach (var (key, debugAction) in EditorHelpKeybinds)
        {
            if (ImGui.IsKeyChordPressed(key))
            {
                debugAction.Invoke(world);
            }
        }

        foreach (var (key, debugAction) in EditorEditKeybinds)
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
            if (ImGui.Begin(windowTitle, ref dontCloseWindow))
            {
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
            }

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
                if (world.Has<Editor_DontShowInLists>(entity))
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
                    ChangeHistory.Push((entity, ComponentPriorToChange_Cached, true));
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