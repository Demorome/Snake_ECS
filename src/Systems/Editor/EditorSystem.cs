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

    MoonTools.ECS.Filter PositionFilter, LevelLayerFilter;

    public Entity? DebugEntity = null; // So we can stick Relations on this to safely track other entities.
    static string DebugEntityTag = "EDITOR";

    TileManipulator TileManipulator;
    MirrorManipulator MirrorManipulator;
    EnemySpawner EnemySpawner;

    public EditorSystem(World world) : base(world)
    {
        PositionFilter = FilterBuilder.Include<Position2D>().Build();
        LevelLayerFilter = FilterBuilder.Include<Editor_LevelLayerID>().Build();

        TileManipulator = new(world);
        MirrorManipulator = new(world);
        EnemySpawner = new(world);
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

        HandleEntitySelectionMode();
        HandleLevelEditor();
    }

    private enum Prefab
    {
        None = 0,
        StaticLevelMirror,
        FrogEnemy
    }
    private Prefab PrefabToSpawn = Prefab.None;

    public Entity SpawnPrefab(Position2D pos)
    {
        // FIXME: Add to change history!

        switch (PrefabToSpawn)
        {
            case Prefab.StaticLevelMirror:
                return MirrorManipulator.CreateStaticLevelMirror(pos);
            case Prefab.FrogEnemy:
                return EnemySpawner.SpawnFrog(pos);
        }
        throw new Exception("Failed to spawn prefab");
    }

    void ShowPrefabSpawner()
    {
        if (ImGui.Begin("Prefab Objects"u8))
        {
            foreach (Prefab prefab in Enum.GetValues(typeof(Prefab)))
            {
                if (prefab == Prefab.None)
                {
                    continue;
                }

                bool isSelected = PrefabToSpawn == prefab;
                ImGui.PushStyleColor(ImGuiCol.Header, Color.Green.ToVector4());
                if (ImGui.Selectable(prefab.ToString(), isSelected))
                {
                    PrefabToSpawn = isSelected ? Prefab.None : prefab;
                }
                ImGui.PopStyleColor();
            }

            // TODO: Once button to spawn a prefab entity is pressed, make it appear transparent below cursor.
            if (PrefabToSpawn != Prefab.None)
            {
                if (!ImGui.GetIO().WantCaptureMouse
                    && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    SpawnPrefab(Input.WorldMousePosition);
                }
            } 
        }
        ImGui.End();
    }



    List<LevelLayer> LevelLayers = new(); // FIXME: Load from level data

    int ActiveLayerID = -1;
    int SelectedLayerID = -1;
    public bool IsActiveLayerTiled => LevelLayers[ActiveLayerID].IsTiled;
    public float ActiveLayerDepth => LevelLayers[ActiveLayerID].Depth;

    static int LayerImageToReplaceID = -1;

    class SelectedImagesToPaint
    {
        // NOTE: This is a flat 2D array. See NumColumns for the column count.
        // The `bool` is for "isNotFiller"; some may be filler in an non-square selection scheme.
        public List<(int, bool)> LayerImageIDs = new();
        public int? FirstValidLayerImageID
        {
            get
            {
                int? result = null;
                foreach (var (layerImageID, isNotFiller) in LayerImageIDs)
                {
                    if (isNotFiller)
                    {
                        return layerImageID;
                    }
                }
                return result;
            }
        }
        public int NumColumns = -1;
    }
    static SelectedImagesToPaint ImagesToPaint = null;

    public List<(SpriteAnimation, Color, Position2D, Editor_LayerImageID)> GetLayerImagesToPaint()
    {
        if (!IsInLevelEditor || ActiveLayerID == -1 || ImagesToPaint == null || ImGui.GetIO().WantCaptureMouse)
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
        foreach (var (layerImageID, isNotFiller) in ImagesToPaint.LayerImageIDs)
        {
            Position2D worldPos = mouseWorldPos;

            var currentTile = nextTile;

            if (activeLayer.IsTiled)
            {
                nextTile.X += 1;
                column += 1;
                column %= ImagesToPaint.NumColumns;
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
                if (!isNotFiller || IsEmptyTile(activeLayer.Images[layerImageID].Item1))
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

    class TileLayerMenu
    {
        // Visual constants
        static readonly Color SelectedOutlineColor = Color.Chocolate;
        static readonly Color HoveredOutlineColor = Color.White;
        static readonly Color ReplacingOutlineColor = Color.Red;

        // Options
        private bool ShowTileLayerMenuGrid = true;

        // State
        private int HoveredTileButtonIndex = -1;
        private float TileLayerMenuBottomPortionWidth = 1;

        // For DrawTileSetSelectionPopup
        const string TileSpritePrefix = "Tile_";
        const string TileSetPrefix = "TileSet_";
        unsafe ImGuiTextFilterPtr TileSearchFilter = new(ImGui.ImGuiTextFilter(""u8));
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

        private bool IsLayerImageInvalid(int layerImageID, LevelLayer levelLayer)
        {
            return levelLayer.Images[layerImageID].Item1.SpriteAnimationInfoID == SpriteAnimations.EditorTile_InvalidTile.ID;
        }

        void UpdateMultiImagePaintSelection(LevelLayer levelLayer, int? toAddIndex = null, int? toRemoveIndex = null)
        {
            // LayerImageIDs may be invalid here, for odd selection schemes.
            // Ex: picking 2 sprites that are diagonal from each other.
            // This would produce a 2x2 selection scheme, with 2 tiles being 'invalid' (empty).
            List<int> validLayerImageIDs = new();
            foreach (var (layerImageID, isNotFiller) in ImagesToPaint.LayerImageIDs)
            {
                if (isNotFiller)
                {
                    validLayerImageIDs.Add(layerImageID);
                }
            }
            ImagesToPaint.LayerImageIDs.Clear();

            if (toAddIndex.HasValue)
            {
                validLayerImageIDs.Add(toAddIndex.Value);
            }
            if (toRemoveIndex.HasValue)
            {
                validLayerImageIDs.Remove(toRemoveIndex.Value);
            }

            if (validLayerImageIDs.Count == 0)
            {
                ImagesToPaint = null;
                return;
            }

            var top = int.MaxValue;
            var bottom = int.MinValue;
            var left = int.MaxValue;
            var right = int.MinValue;

            foreach (var layerImageID in validLayerImageIDs)
            {
                var col = layerImageID % levelLayer.ImagesPerRow;
                var row = layerImageID / levelLayer.ImagesPerRow;

                top = int.Min(top, row);
                bottom = int.Max(bottom, row);
                left = int.Min(left, col);
                right = int.Max(right, col);
            }

            ImagesToPaint.NumColumns = right - left + 1;

            for (int row = top; row <= bottom; ++row)
            {
                for (int col = left; col <= right; ++col)
                {
                    var currentImageLayerPos = row * levelLayer.ImagesPerRow + col;
                    if (validLayerImageIDs.Contains(currentImageLayerPos))
                    {
                        ImagesToPaint.LayerImageIDs.Add((currentImageLayerPos, true));
                    }
                    else
                    {
                        ImagesToPaint.LayerImageIDs.Add((currentImageLayerPos, false));
                    }
                }
            }
        }

        private void HandleMultiSelectRequests(ImGuiMultiSelectIOPtr multiSelectIO, LevelLayer levelLayer)
        {
            for (int requestNum = 0; requestNum < multiSelectIO.Requests.Size; ++requestNum)
            {
                var request = multiSelectIO.Requests[requestNum];

                if (request.Type == ImGuiSelectionRequestType.SetAll)
                {
                    if (request.Selected != 0) // Select all
                    {
                        if (ImagesToPaint == null)
                        {
                            ImagesToPaint = new();
                        }
                        else
                        {
                            ImagesToPaint.LayerImageIDs.Clear();
                        }

                        for (int id = 0; id < levelLayer.Images.Count; ++id)
                        {
                            ImagesToPaint.LayerImageIDs.Add((id, true));
                        }
                    }
                    else // Unselect all
                    {
                        if (ImagesToPaint != null)
                        {
                            ImagesToPaint.LayerImageIDs.Clear();
                            ImagesToPaint = null;
                        }
                    }
                }
                else if (request.Type == ImGuiSelectionRequestType.SetRange)
                {
                    for (int id = (int)request.RangeFirstItem; id <= (int)request.RangeLastItem; ++id)
                    {
                        if (IsLayerImageInvalid(id, levelLayer))
                        {
                            // Can't interact with an invalid image.
                            continue;
                        }

                        if (request.Selected == 0) // selection removed
                        {
                            UpdateMultiImagePaintSelection(levelLayer, null, id);
                        }
                        else // selection added
                        {
                            if (ImagesToPaint == null)
                            {
                                ImagesToPaint = new();
                                ImagesToPaint.NumColumns = 1;
                                ImagesToPaint.LayerImageIDs.Add((id, true));
                            }
                            else
                            {
                                UpdateMultiImagePaintSelection(levelLayer, id, null);
                            }
                        }
                    }
                }
                else
                {
                    throw new NotImplementedException("Unexpected selection request type!");
                }
            }
        }

        void DrawTileSpriteReplacementsPopup(LevelLayer levelLayer, World World)
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
                ImGui.EndPopup();
            }
            else
            {
                LayerImageToReplaceID = -1;
            }
        }

        public void Show(LevelLayer tileLayer, World World)
        {
            if (ImGui.InputInt("Tiles per row", ref tileLayer.ImagesPerRow))
            {
                tileLayer.ImagesPerRow = int.Max(1, tileLayer.ImagesPerRow);
                // Reset the selected tiles since if multiple were selected, the selection would change in a bizarre way.
                ImagesToPaint = null;
            }

            if (ImGui.InputFloat("Preview Scale", ref tileLayer.PreviewScaleMult, 1f))
            {
                tileLayer.PreviewScaleMult = float.Max(0.5f, tileLayer.PreviewScaleMult);
                if (tileLayer.PreviewScaleMult > 1f)
                {
                    tileLayer.PreviewScaleMult = float.Floor(tileLayer.PreviewScaleMult);
                }
            }

            ImGui.Checkbox("Show Grid", ref ShowTileLayerMenuGrid);

            var scalingFactor = ImGui.GetWindowViewport().Size / Dimensions.GAME_DIMENSIONS * tileLayer.PreviewScaleMult;
            var tileSizeScaled = Dimensions.TILE_DIMENSIONS * scalingFactor;

            ImGui.Separator();

            if (ImGui.BeginChild("##TileView", new Vector2(-1, -TileLayerMenuBottomPortionWidth),
                ImGuiChildFlags.AlwaysAutoResize | ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY,
                ImGuiWindowFlags.AlwaysHorizontalScrollbar | ImGuiWindowFlags.AlwaysVerticalScrollbar))
            {
                // Multi-selection code based off of Dear Imgui's Example Assets Browser: https://github.com/ocornut/imgui/blob/2ab3946ecb12962eff96c9bc13ef83d403c84dd8/imgui_demo.cpp#L10538
                var multiSelectIO = ImGui.BeginMultiSelect(
                    ImGuiMultiSelectFlags.BoxSelect2D // enable drag-selection
                    | ImGuiMultiSelectFlags.ClearOnEscape
                    | ImGuiMultiSelectFlags.ClearOnClickVoid
                    | ImGuiMultiSelectFlags.NavWrapX, // Enable keyboard wrapping on X axis
                    0, // FIXME: Pass # of selected items!
                    tileLayer.Images.Count
                );
                HandleMultiSelectRequests(multiSelectIO, tileLayer);

                // Draw with 1 pixel gaps between sprites.
                // Helpful explanation: https://github.com/ocornut/imgui/issues/4216#issuecomment-860007592
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2.0f, 2.0f));
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, 0f));
                //ImGui.PushStyleVar(ImGuiStyleVar.ImageBorderSize, 1f);
                const int numStyleVars = 2;

                for (int i = 0; i < tileLayer.Images.Count; ++i)
                {
                    var (sprite, colorBlend) = tileLayer.Images[i];
                    var tintColor = tileLayer.MixLayerColorWithImageColor(colorBlend);

                    // FIXME: Allow sprite animations to play (simulate frame countdown?)
                    var currentFrame = sprite.CurrentSprite;

                    bool wasSelected = ImagesToPaint != null && ImagesToPaint.LayerImageIDs.Contains((i, true));
                    bool wasHovered = i == HoveredTileButtonIndex;
                    bool isReplacing = i == LayerImageToReplaceID;

                    Vector2 posToOverlap = ImGui.GetCursorScreenPos();
                    var origin = sprite.Origin * scalingFactor;
                    var offset = -origin - new Vector2(currentFrame.FrameRect.X, currentFrame.FrameRect.Y) * scalingFactor;
                    ImGui.SetCursorScreenPos(posToOverlap + offset + (tileSizeScaled / 2));
                    ImGui.SetNextItemAllowOverlap();

                    // Draw tile sprite
                    ImGuiExtensions.ImageWithBg(
                        currentFrame.Texture,
                        currentFrame.SliceSize * scalingFactor,
                        currentFrame.UV.LeftTop,
                        currentFrame.UV.RightBottom,
                        Color.Transparent.ToVector4(),
                        tintColor.ToVector4(),
                        ImGuiBackend.SamplerType.PointClamp
                    );

                    // Draw tile outline.
                    if (ShowTileLayerMenuGrid || wasSelected || wasHovered || isReplacing)
                    {
                        uint gridColorPacked;
                        if (isReplacing)
                        {
                            gridColorPacked = ReplacingOutlineColor.PackedValue();
                        }
                        else if (wasHovered)
                        {
                            var hoveredColor = HoveredOutlineColor;
                            if (wasSelected)
                            {
                                // Make it clear if a hovered tile is selected or not.
                                hoveredColor = Color.Lerp(hoveredColor, SelectedOutlineColor, 0.5f);
                            }
                            gridColorPacked = hoveredColor.PackedValue();
                        }
                        else if (wasSelected)
                        {
                            gridColorPacked = SelectedOutlineColor.PackedValue();
                        }
                        else if (ShowTileLayerMenuGrid)
                        {
                            gridColorPacked = new Color(GridLineColor).PackedValue();
                        }
                        else
                        {
                            throw new Exception("Unhandled case!");
                        }

                        ImGui.SetCursorScreenPos(posToOverlap);

                        // Reduce the thickness when zooming out. Probably gets set to a minimum of 1 by ImGui anyways, so oops.
                        float thickness = float.Min(1f, tileLayer.PreviewScaleMult);

                        // Draw a square outline for the tile sprite.
                        ImGui.GetWindowDrawList().AddRect(posToOverlap, posToOverlap + tileSizeScaled,
                            gridColorPacked, 0.0f, ImDrawFlags.None, thickness);
                    }

                    ImGui.SetCursorScreenPos(posToOverlap);

                    ImGui.SetNextItemSelectionUserData(i); // needed for MultiSelect
                    ImGui.PushID(i);
                    // FIXME: Make selectable invisible (remove background col and border highlight)!
                    // FIXME: Crashes when using empty u8 string!
                    ImGui.Selectable("", wasSelected, ImGuiSelectableFlags.None, tileSizeScaled);
                    ImGui.PopID();

                    if (ImGui.IsItemHovered())
                    {
                        HoveredTileButtonIndex = i;

                        // Right-clicking on a sprite opens a menu to replace the sprite for any other "Tile"-named sprite
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                        {
                            ImGui.OpenPopup("##SelectTileSprite");
                            LayerImageToReplaceID = i;
                        }
                    }

                    if (((i + 1) % tileLayer.ImagesPerRow) != 0)
                    {
                        ImGui.SameLine();
                    }
                }

                ImGui.PopStyleVar(numStyleVars);

                multiSelectIO = ImGui.EndMultiSelect();
                HandleMultiSelectRequests(multiSelectIO, tileLayer);
            }

            if (LayerImageToReplaceID != -1)
            {
                DrawTileSpriteReplacementsPopup(tileLayer, World);
            }
            if (HoveredTileButtonIndex != -1)
            {
                // FIXME: Set to -1 if no longer hovering anything!!!!
            }
            ImGui.EndChild();

            var startHeight = ImGui.GetCursorScreenPos().Y;
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
            bool noSelectedImages = ImagesToPaint == null || ImagesToPaint.LayerImageIDs.Count <= 0;
            if (noSelectedImages)
            {
                ImGui.BeginDisabled();
            }
            if (ImGui.Button("Delete"))
            {
                // FIXME: Undo/Redo support!
                for (int nthImageToPaint = ImagesToPaint.LayerImageIDs.Count - 1; nthImageToPaint >= 0; --nthImageToPaint)
                {
                    var (layerImageID, isNotFiller) = ImagesToPaint.LayerImageIDs[nthImageToPaint];
                    if (isNotFiller)
                    {
                        tileLayer.DeleteLayerImage(layerImageID, World);
                    }
                }

                // Reset selections.
                ImagesToPaint.LayerImageIDs.Clear();
                ImagesToPaint = null;
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
            var firstValidLayerImageID = ImagesToPaint == null ? null : ImagesToPaint.FirstValidLayerImageID;
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
                if (ImagesToPaint != null)
                {
                    foreach (var (layerImageID, isNotFiller) in ImagesToPaint.LayerImageIDs)
                    {
                        if (isNotFiller)
                        {
                            tileLayer.ChangeTileColorBlend(new Editor_LayerImageID(layerImageID),
                                new Color(tileColor), World);
                        }
                    }
                }
            }
            if (noSelectedImages)
            {
                ImGui.EndDisabled();
            }

            var endHeight = ImGui.GetCursorScreenPos().Y;
            TileLayerMenuBottomPortionWidth = endHeight - startHeight;
        }
    }
    static TileLayerMenu TileLayerMenuStatic = new();

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
                        ImagesToPaint = null;
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
                    ImagesToPaint = null;
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
                        TileLayerMenuStatic.Show(layer, World);
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

            ImGui.Checkbox("Show Grid?", ref ShowGrid);
            ImGui.ColorEdit4("Grid Line Color", ref GridLineColor);

            // TODO: Snap to grid option? Not sure if I should support going off-grid yet.
            //ImGui.Checkbox("Snap to Grid", ref SnapEntitiesToGrid);
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

        ShowPrefabSpawner();

        DrawLevelEditorMainWindow();
        ShowLevelLayerOptions();
        
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

                            // Replace a tile if one is already painted in at this level layer.
                            bool spawn = true;
                            foreach (var entity in activeLayer.CachedEntities)
                            {
                                if (Get<Position2D>(entity) == tileWorldPos)
                                {
                                    if (Get<Editor_LayerImageID>(entity) != layerImageID)
                                    {
                                        // FIXME: Undo/Redo support! How to group this change w/ the creation of the tile below?
                                        Destroy(entity);
                                    }
                                    else
                                    {
                                        spawn = false;
                                    }
                                    // Assume there can't be any other entities on this layer at this tile pos.
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
                    var worldRect = rect.Value.GetWorldRect(Get<Position2D>(entity));
                    VisualEntitiesSpatialHash.Insert(entity, worldRect);
                }
            }

            // Check what entities the mouse is hovering over.
            // FIXME: Make this ignore entities that aren't in the Level Editor's currently active Editor Layer?
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