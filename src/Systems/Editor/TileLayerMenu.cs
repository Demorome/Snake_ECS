#if DEBUG

using System;
using System.Collections.Generic;
using System.Numerics;
using Hexa.NET.ImGui;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Data;
using RollAndCash.Relations;
using RollAndCash.Systems;

namespace RollAndCash.Editor;

public class SelectedImagesToPaint
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
};

public class TileLayerMenu
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
    public static SelectedImagesToPaint ImagesToPaint = null;
    static int LayerImageToReplaceID = -1;

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
        return levelLayer.Images[layerImageID].Item1.SpriteAnimationInfoID
            == SpriteAnimations.EditorTile_InvalidTile.ID;
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
                        gridColorPacked = new Color(LevelEditorManipulator.GridLineColor).PackedValue();
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
#endif