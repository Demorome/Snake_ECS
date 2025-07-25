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
            LevelLayers[layerID.ID].CachedEntities.Add(entity);
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

    static int LayerImageToReplaceID = -1;
    // NOTE: This is a flat 2D array. See NumColumnsToPaint for the column count.
    static List<(int, bool)> LayerImageIDsToPaint = new(); // FIXME: Make flat 2D arrays with this!
    static int? FirstValidLayerImageID
    {
        get
        {
            int? result = null;
            foreach (var (layerImageID, isValid) in LayerImageIDsToPaint)
            {
                if (isValid)
                {
                    return layerImageID;
                }
            }
            return result;
        }
    }
    
    static int NumColumnsToPaint = -1;
    public List<(SpriteAnimation, Color, Position2D, Editor_LayerImageID)> GetLayerImagesToPaint()
    {
        if (!IsInLevelEditor || ActiveLayerID == -1 || LayerImageIDsToPaint.Count == 0)
        {
            return new();
        }
        var result = new List<(SpriteAnimation, Color, Position2D, Editor_LayerImageID)>();

        var activeLayer = LevelLayers[ActiveLayerID];
        var mouseWorldPos = Input.WorldMousePosition;
        var maybeHoveredOverTile = TileManipulator.GetTilePos(mouseWorldPos);
        if (activeLayer.IsTiled && !maybeHoveredOverTile.HasValue)
        {
            return new();
        }

        var startingTile = maybeHoveredOverTile.Value;
        var nextTile = startingTile;
        int column = 0;
        foreach (var (layerImageID, isValid) in LayerImageIDsToPaint)
        {
            Position2D worldPos = mouseWorldPos;

            var currentTile = nextTile;

            if (activeLayer.IsTiled)
            {
                nextTile.X += 1;
                column += 1;
                column %= NumColumnsToPaint;
                if (column == 0)
                {
                    nextTile.X = startingTile.X;
                    nextTile.Y += 1;
                }

                if (!TileManipulator.IsTilePosValid(currentTile))
                {
                    continue;
                }

                worldPos = TileManipulator.TilePosToWorldPos_Centered(currentTile);

                // Tile may be invalid here, for odd selection schemes.
                // Ex: picking 2 sprites that are diagonal from each other.
                // This would produce a 2x2 selection scheme, with 2 tiles being 'invalid' (empty).
                // Or, the tile may actually be an empty filler tile, with a valid LayerImageID.
                if (!isValid || IsEmptyTile(activeLayer.Images[layerImageID].Item1))
                {
                    continue;
                }
            }

            var (sprite, imageColor) = activeLayer.Images[layerImageID];
            imageColor = activeLayer.MixLayerColorWithImageColor(imageColor);
            result.Add((sprite, imageColor, worldPos, new Editor_LayerImageID(layerImageID)));
        }

        if (result.Count > 1 && !activeLayer.IsTiled)
        {
            throw new Exception("Should only be creating 1 image here...");
        }

        return result;
    }

    static bool IsEmptyTile(SpriteAnimation sprite)
    {
        return sprite.SpriteAnimationInfoID == SpriteAnimations.EditorTile_EmptyTile.ID
            || sprite.CurrentSprite.UV == SpriteAnimations.EditorTile_EmptyTile.Frames[0].UV;
    }

    const string TileSpritePrefix = "Tile_";
    const string TileSetPrefix = "TileSet_";
    unsafe static ImGuiTextFilterPtr TileSearchFilter = new(ImGuiNative.ImGuiTextFilter_ImGuiTextFilter(null));

    void DrawTileSetSelectionPopup(LevelLayer levelLayer)
    {
        if (ImGui.BeginPopup("##AddTileset"))
        {
            ImGui.Text("Select TileSet");
            ImGui.Separator();
            TileSearchFilter.Draw("Search");

            foreach (var spriteName in SpriteAnimations.Names)
            {
                if (!spriteName.ToLower().StartsWith(TileSetPrefix.ToLower()))
                {
                    continue;
                }

                if (TileSearchFilter.PassFilter(spriteName))
                {
                    if (ImGui.Selectable(spriteName))
                    {
                        var tileSetSpriteID = SpriteAnimations.NameToInfoMap[spriteName].ID;
                        var tileSetSpriteAnimInfo = SpriteAnimationInfo.FromID(tileSetSpriteID);
                        var defaultColor = Color.White;

                        // Add a bunch of SpriteAnimation-s based on each frame of the tileset
                        for (int i = 0; i < tileSetSpriteAnimInfo.Frames.Length; ++i)
                        {
                            levelLayer.Images.Add(
                                (SpriteAnimation.ForceFrame(tileSetSpriteAnimInfo, i), defaultColor)
                            );
                        }
                        break;
                    }
                }
            }

            ImGui.EndPopup();
        }
    }

    void DrawTileSpriteReplacementsPopup(LevelLayer levelLayer)
    {
        if (ImGui.BeginPopup("##SelectTileSprite"))
        {
            ImGui.Text("Select Tile Sprite Replacement");
            ImGui.Separator();
            TileSearchFilter.Draw("Search");

            foreach (var spriteName in SpriteAnimations.Names)
            {
                if (!spriteName.ToLower().StartsWith(TileSpritePrefix.ToLower()))
                {
                    continue;
                }

                if (TileSearchFilter.PassFilter(spriteName))
                {
                    if (ImGui.Selectable(spriteName))
                    {
                        var spriteID = SpriteAnimations.NameToInfoMap[spriteName].ID;
                        var spriteAnimInfo = SpriteAnimationInfo.FromID(spriteID);
                        var sprite = new SpriteAnimation(spriteAnimInfo);
                        levelLayer.ReplaceImage(LayerImageToReplaceID, sprite, World);
                        LayerImageToReplaceID = -1;
                        break;
                    }
                }
            }
        }
        else
        {
            LayerImageToReplaceID = -1;
        }
        ImGui.EndPopup();
    }

    void UpdateMultiImagePaintSelection(LevelLayer levelLayer, int? toAddIndex = null)
    {
        // LayerImageIDs may be invalid here, for odd selection schemes.
        // Ex: picking 2 sprites that are diagonal from each other.
        // This would produce a 2x2 selection scheme, with 2 tiles being 'invalid' (empty).
        List<int> validLayerImageIDs = new();
        foreach (var (layerImageID, isValid) in LayerImageIDsToPaint)
        {
            if (isValid)
            {
                validLayerImageIDs.Add(layerImageID);
            }
        }
        LayerImageIDsToPaint.Clear();

        if (validLayerImageIDs.Count == 0)
        {
            NumColumnsToPaint = -1;
            return;
        }

        // Assumes the list was ordered.
        var firstIndex = validLayerImageIDs[0];
        var lastIndex = validLayerImageIDs[validLayerImageIDs.Count - 1];

        var left = firstIndex % levelLayer.ImagesPerRow;
        var top = firstIndex / levelLayer.ImagesPerRow;
        var right = lastIndex % levelLayer.ImagesPerRow;
        var bottom = lastIndex / levelLayer.ImagesPerRow;

        if (toAddIndex.HasValue)
        {
            validLayerImageIDs.Add(toAddIndex.Value);

            left = int.Min(left, toAddIndex.Value % levelLayer.ImagesPerRow);
            top = int.Min(top, toAddIndex.Value / levelLayer.ImagesPerRow);
            right = int.Max(right, toAddIndex.Value % levelLayer.ImagesPerRow);
            bottom = int.Max(bottom, toAddIndex.Value / levelLayer.ImagesPerRow);
        }

        var numColumns = right - left + 1;
        NumColumnsToPaint = numColumns;

        for (int row = top; row <= bottom; ++row)
        {
            for (int col = left; col <= right; ++col)
            {
                var currentImageLayerPos = row * levelLayer.ImagesPerRow + col;
                if (validLayerImageIDs.Contains(currentImageLayerPos))
                {
                    LayerImageIDsToPaint.Add((currentImageLayerPos, true));
                }
                else
                {
                    LayerImageIDsToPaint.Add((currentImageLayerPos, false));
                }
            }
        }
    }

    void ShowTileLayerMenu(LevelLayer tileLayer)
    {
        var imageBgColor = Color.Transparent;

        var scalingFactor = ImGui.GetWindowViewport().Size / Dimensions.GAME_DIMENSIONS;
        var tileSizeScaled = Dimensions.TILE_DIMENSIONS * scalingFactor;
        
        // Draw with 1 pixel gaps between sprites.
        // Helpful explanation: https://github.com/ocornut/imgui/issues/4216#issuecomment-860007592
        // FIXME: How to have gray outline but not make the background for the image gray??
        ImGui.PushStyleColor(ImGuiCol.Button, Color.Transparent.ToVector4());
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2.0f, 2.0f));
        //ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(1.0f, 1.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, 0f));

        for (int i = 0; i < tileLayer.Images.Count; ++i)
        {
            var (sprite, colorBlend) = tileLayer.Images[i];

            bool invalid = sprite.SpriteAnimationInfoID == SpriteAnimations.EditorTile_InvalidTile.ID;

            // FIXME: Allow sprite animations to play (simulate frame countdown?)
            var currentFrame = sprite.CurrentSprite;

            bool wasSelected = LayerImageIDsToPaint.Contains((i, true));

            if (i == LayerImageToReplaceID)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Color.Red.ToVector4());
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Color.Red.ToVector4());
            }
            else if (wasSelected)
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
                tileLayer.MixLayerColorWithImageColor(colorBlend).ToVector4(),
                ImGuiBackend.SamplerType.PointClamp
                ))
            {
                if (!invalid)
                {
                    if (wasSelected)
                    {
                        LayerImageIDsToPaint.Remove((i, true));
                        if (LayerImageIDsToPaint.Count == 0)
                        {
                            NumColumnsToPaint = -1;
                        }
                        else
                        {
                            UpdateMultiImagePaintSelection(tileLayer);
                        }
                    }
                    else
                    {
                        if (LayerImageIDsToPaint.Count >= 1 && ImGui.IsKeyDown(ImGuiKey.ModCtrl))
                        {
                            UpdateMultiImagePaintSelection(tileLayer, i);
                        }
                        else
                        {
                            LayerImageIDsToPaint.Clear();
                            LayerImageIDsToPaint.Add((i, true));
                            NumColumnsToPaint = 1;
                        }
                    }
                }
            }

            if (wasSelected || i == LayerImageToReplaceID)
            {
                ImGui.PopStyleColor(2);
            }

            // Right-clicking on a sprite opens a menu to replace the sprite for any other "Tile"-named sprite
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup("##SelectTileSprite");
                LayerImageToReplaceID = i;
            }

            if (((i + 1) % tileLayer.ImagesPerRow) != 0)
            {
                ImGui.SameLine();
            }
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();

        ImGui.NewLine();
        ImGui.Separator();

        // FIXME: Undo/Redo support!
        if (ImGui.Button("Add Tile"))
        {
            tileLayer.Images.Add((new SpriteAnimation(SpriteAnimations.EditorTile_EmptyTile), Color.White));
        }
        ImGui.SameLine();
        if (ImGui.Button("Add TileSet"))
        {
            ImGui.OpenPopup("##AddTileset");
        }
        DrawTileSetSelectionPopup(tileLayer);
        ImGui.SameLine();
        bool noSelectedImages = LayerImageIDsToPaint.Count <= 0;
        if (noSelectedImages)
        {
            ImGui.BeginDisabled();
        }
        if (ImGui.Button("Delete"))
        {
            // FIXME: Implement!

            // Reset selections.
            NumColumnsToPaint = -1;
            LayerImageIDsToPaint.Clear();
        }
        if (noSelectedImages)
        {
            ImGui.EndDisabled();
        }

        var colorBlendVec = tileLayer.ColorBlend.ToVector4();
        if (ImGui.ColorEdit4("Layer Color Blend", ref colorBlendVec))
        {
            tileLayer.ChangeLayerColorBlend(new Color(colorBlendVec), World);
        }

        var tileColor = Color.White.ToVector4();
        var firstValidLayerImageID = FirstValidLayerImageID;
        noSelectedImages = firstValidLayerImageID == null;
        if (noSelectedImages)
        {
            ImGui.BeginDisabled();
        }
        else
        {
            tileColor = tileLayer.Images[firstValidLayerImageID.Value].Item2.ToVector4();
        }
        if (ImGui.ColorEdit4("Tile Color Blend", ref tileColor))
        {
            foreach (var (layerImageID, isValid) in LayerImageIDsToPaint)
            {
                if (isValid)
                {
                    tileLayer.ChangeTileColorBlend(new Editor_LayerImageID(layerImageID),
                        new Color(tileColor), World);
                }
            }
        }
        if (noSelectedImages)
        {
            ImGui.EndDisabled();
        }

        if (ImGui.InputInt("Tiles per row", ref tileLayer.ImagesPerRow))
        {
            tileLayer.ImagesPerRow = int.Max(1, tileLayer.ImagesPerRow);
            // Reset the selected tiles since if multiple were selected, the selection would change in a bizarre way.
            NumColumnsToPaint = -1;
            LayerImageIDsToPaint.Clear();
        }
        

        if (LayerImageToReplaceID != -1)
        {
            DrawTileSpriteReplacementsPopup(tileLayer);
        }
    }

    public int HoveredOverLayerID = -1;
    public LevelLayer HoveredOverLayer => HoveredOverLayerID == -1 ? null : LevelLayers[HoveredOverLayerID];

    void ShowLevelLayerOptions()
    {
        HoveredOverLayerID = -1;

        if (ImGui.Begin("Level Layers"))
        {
            for (int i = 0; i < LevelLayers.Count; ++i)
            {
                var layer = LevelLayers[i];
                var isVisible = layer.IsVisible;
                if (ImGui.Checkbox("##" + layer.Name + "Visibility", ref isVisible))
                {
                    layer.ToggleVisibility(World, DebugEntity.Value);
                }
                ImGui.SameLine();

                if (ImGui.Selectable(layer.Name, SelectedLayerID == i, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    SelectedLayerID = i;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        ActiveLayerID = i;
                        LayerImageIDsToPaint.Clear();
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    HoveredOverLayerID = i;
                }

                ImGui.SameLine();
                ImGui.TextColored(Color.Green.ToVector4(),
                    $"\tDepth: {layer.Depth}" + (layer.IsDepthLocked ? " (Locked)" : ""));

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
            bool disabled = false;
            if (SelectedLayerID == -1)
            {
                ImGui.BeginDisabled();
                disabled = true;
            }
            if (ImGui.Button("Delete"))
            {
                if (ActiveLayerID == SelectedLayerID)
                {
                    ActiveLayerID = -1;
                    LayerImageIDsToPaint.Clear();
                }
                else if (ActiveLayerID > SelectedLayerID)
                {
                    ActiveLayerID -= 1;
                }

                if (HoveredOverLayerID == SelectedLayerID)
                {
                    HoveredOverLayerID = -1;
                }
                else if (HoveredOverLayerID > SelectedLayerID)
                {
                    HoveredOverLayerID -= 1;
                }

                // Deleting a layer deletes all entities in it.
                LevelLayer.DeleteLayerCleanup(new Editor_LevelLayerID(SelectedLayerID), LevelLayers, World);
                SelectedLayerID = -1;
            }

            ImGui.SameLine();
            if (ImGui.Button("Rename"))
            {
                ImGui.OpenPopup($"RenameLayer{SelectedLayerID}");
            }

            ImGui.SameLine();
            if (SelectedLayerID != -1 && LevelLayers[SelectedLayerID].IsDepthLocked)
            {
                ImGui.BeginDisabled();
                disabled = true;
            }
            if (ImGui.Button("Change Depth"))
            {
                ImGui.OpenPopup($"ChangeLayerDepth");
            }
            if (ImGui.BeginPopup($"ChangeLayerDepth"))
            {
                var depth = LevelLayers[SelectedLayerID].Depth;
                if (ImGui.InputFloat("Depth", ref depth))
                {
                    LevelLayers[SelectedLayerID].ChangeLayerDepth(depth, World);
                }
                ImGui.EndPopup();
            }
            if (disabled)
            {
                ImGui.EndDisabled();
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
        var imagesToPaint = GetLayerImagesToPaint();
        if (imagesToPaint != null && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            List<Entity> paintedEntities = new();

            switch (activeLayer.LayerType)
            {
                case LevelLayer.LevelLayerTypes.VisualTile:
                case LevelLayer.LevelLayerTypes.SolidTile:
                    if (imagesToPaint.Count == 1 || ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        foreach (var (imageSprite, imageColor, imagePos, layerImageID) in imagesToPaint)
                        {
                            var tileWorldPos = TileManipulator.TilePosToWorldPos_Centered(HoveredOverTilePosition.Value);

                            // Don't spawn anything if tile is already painted in at this level layer.
                            bool spawn = true;
                            foreach (var entity in activeLayer.CachedEntities)
                            {
                                if (Get<Position2D>(entity) == tileWorldPos
                                    && Get<Editor_LayerImageID>(entity) == layerImageID)
                                {
                                    spawn = false;
                                    break;
                                }
                            }

                            if (spawn)
                            {
                                Entity newEntity;
                                if (activeLayer.LayerType == LevelLayer.LevelLayerTypes.SolidTile)
                                {
                                    newEntity = TileManipulator.SpawnSolidTile(tileWorldPos, imageSprite);
                                }
                                else
                                {
                                    newEntity = CreateEntity("Visual Tile");
                                    Set(newEntity, tileWorldPos);
                                    Set(newEntity, imageSprite);
                                }

                                Set(newEntity, new ColorBlend(imageColor));
                                Set(newEntity, imagePos);
                                Set(newEntity, layerImageID);

                                paintedEntities.Add(newEntity);
                            }
                        }
                    }
                    break;
                case LevelLayer.LevelLayerTypes.Image:
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        var (spriteAnim, color, position, layerImageID) = imagesToPaint[0];

                        Entity newEntity = CreateEntity("Image");
                        Set(newEntity, spriteAnim);
                        Set(newEntity, new ColorBlend(color));
                        Set(newEntity, position);
                        Set(newEntity, layerImageID);
                    }
                    break;
            }

            foreach (var entity in paintedEntities)
            {
                Set(entity, new Editor_LevelLayerID(ActiveLayerID));
                Set(entity, new Depth(activeLayer.Depth));

                // FIXME: Group together multiple entities created in a single paintbrush stroke for Undo.
                UndoRedo.StoreEntityCreateHistory(entity, World);
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
                        // FIXME: Group together deletions done while holding the mouse down!
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
            var currentSprite = spriteAnim.CurrentSprite;
            var rect = currentSprite.FrameRect;
            var origin = spriteAnim.Origin;
            
            // FIXME: Account for rotation and scale?
            var offset = -origin - new Vector2(currentSprite.FrameRect.X, currentSprite.FrameRect.Y);
            return new Rectangle(
                (int)(rect.X + offset.X),
                (int)(rect.Y + offset.Y),
                rect.W,
                rect.H
            );
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