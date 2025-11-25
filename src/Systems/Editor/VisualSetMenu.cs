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

// Visual sets, like TileSets and ImageSets, are displayed here.
// You can also select parts of the sets to paint them in-level later.
public class VisualSetMenu
{
    // Visual constants
    static readonly Color SelectedOutlineColor = Color.Chocolate;
    static readonly Color HoveredOutlineColor = Color.White;
    static readonly Color ModifyingOutlineColor = Color.Red;
    //private const float MenuBottomPortionWidth = 1;

    // State
    public readonly VisualSet VisualSet;
    public VisualSetVariantID CurrentVariant;

    // Only a single VisualSet can make paint selections at any given time.
    // If another becomes active, then it overrides the previous selections.
    public static VisualSet ActiveVisualSet { get; private set; } = null;

    public static SelectedVisualsToPaint SelectedToPaint = new();
    private static PositionInVisualSet? HoveredVisualInSet = null;
    private static PositionInVisualSet? VisualInSetToModify = null;

    public VisualSetMenu(VisualSet visualSet)
    {
        VisualSet = visualSet;
    }

    // TODO: Draw variant tiles if that's the section we're in!
    // TODO: Support drawing (adding) tile metadata!
    // TODO: Support creating new set variants!
    // TODO: Support deleting set variants, w/ warning msg!
    public void Show()
    {
        if (!ImGui.Begin(VisualSet.Name + $"##{VisualSet.Editor_Type}"))
        {
            ImGui.End();
            return;            
        }

        var startHeight = ImGui.GetCursorScreenPos().Y;
        ImGui.Separator();

        if (!VisualSet.Editor_CanResizeColumnCount)
        {
            ImGui.BeginDisabled();
        }
        int imagesPerRow = VisualSet.NumColumns;
        if (ImGui.InputInt($"{VisualSet.Editor_VisualName(true)} per row", ref imagesPerRow))
        {
            VisualSet.Editor_TrySetColumnCount((ushort)int.Max(1, imagesPerRow));
            // Reset the selected tiles, since if multiple were selected, the selection would change in a bizarre way.
            SelectedToPaint = new();
        }
        if (!VisualSet.Editor_CanResizeColumnCount)
        {
            ImGui.EndDisabled();
        }

        if (ImGui.InputFloat("Preview Scale", ref VisualSet.Editor_PreviewScaleMult, 1f))
        {
            VisualSet.Editor_PreviewScaleMult = float.Max(0.5f, VisualSet.Editor_PreviewScaleMult);
            if (VisualSet.Editor_PreviewScaleMult > 1f)
            {
                VisualSet.Editor_PreviewScaleMult = float.Floor(VisualSet.Editor_PreviewScaleMult);
            }
        }

        ImGui.Checkbox("Show Grid", ref VisualSet.Editor_ShowGrid);

        ImGui.Separator();
        var endHeight = ImGui.GetCursorScreenPos().Y;
        var menuBottomPortionWidth = endHeight - startHeight;
        ShowVisualSelection(menuBottomPortionWidth);

        ImGui.End();
    }
    
    private void MaybeChangeActiveSet(ImGuiMultiSelectIOPtr multiSelectIO)
    {
        if (multiSelectIO.Requests.Size != 0)
        {
            ActiveVisualSet = VisualSet;

            SelectedToPaint.ClearSelections();
            HoveredVisualInSet = null ;
            VisualInSetToModify = null;
        }
    }

    private void ShowVisualSelection(float menuBottomPortionWidth)
    {
        var scalingFactor = ImGui.GetWindowViewport().Size / Dimensions.GAME_DIMENSIONS 
            * VisualSet.Editor_PreviewScaleMult
        ;

        // Show every selected Visual from the VisualSet
        if (ImGui.BeginChild("##ViewVisualsFromSet"u8, new Vector2(-1, -menuBottomPortionWidth),
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
                VisualSet.NumVisuals
            );
            MaybeChangeActiveSet(multiSelectIO);
            SelectedToPaint.HandleMultiSelectRequests(multiSelectIO, VisualSet);

            // Draw with 1 pixel gaps between sprites.
            // Helpful explanation: https://github.com/ocornut/imgui/issues/4216#issuecomment-860007592
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2.0f, 2.0f));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, 0f));
            //ImGui.PushStyleVar(ImGuiStyleVar.ImageBorderSize, 1f);
            const int numStyleVars = 2;

            var numVisuals = VisualSet.NumVisuals;
            for (int i = 0; i < numVisuals; ++i)
            {
                var col = (ushort)(i % VisualSet.NumColumns);
                var row = (ushort)(i / VisualSet.NumColumns);
                var posInVisualSet = new PositionInVisualSet(col, row);

                var visualSizeScaled = VisualSet.GetVisualSize(posInVisualSet, CurrentVariant) * scalingFactor;
                var (sprite, colorBlend) = tileLayer.Images[i];
                var tintColor = tileLayer.MixLayerColorWithImageColor(colorBlend);

                // FIXME: Allow sprite animations to play (simulate frame countdown?)
                var currentFrame = sprite.CurrentSprite;

                bool wasSelected = SelectedToPaint != null && SelectedToPaint.Selected.Contains((posInVisualSet, true));
                bool wasHovered = posInVisualSet == HoveredVisualInSet;
                bool isReplacing = posInVisualSet == VisualInSetToModify;

                Vector2 posToOverlap = ImGui.GetCursorScreenPos();
                var origin = sprite.Origin * scalingFactor;
                var offset = -origin - new Vector2(currentFrame.FrameRect.X, currentFrame.FrameRect.Y) * scalingFactor;
                // FIXME
                ImGui.SetCursorScreenPos(posToOverlap + offset + (visualSizeScaled * 0.5f));
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
                if (VisualSet.Editor_ShowGrid || wasSelected || wasHovered || isReplacing)
                {
                    uint gridColorPacked;
                    if (isReplacing)
                    {
                        gridColorPacked = ModifyingOutlineColor.PackedValue();
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
                    else if (VisualSet.Editor_ShowGrid)
                    {
                        gridColorPacked = new Color(LevelEditorManipulator.GridLineColor).PackedValue();
                    }
                    else
                    {
                        throw new Exception("Unhandled case!");
                    }

                    ImGui.SetCursorScreenPos(posToOverlap);

                    // Reduce the thickness when zooming out. 
                    // FIXME: Probably gets set to a minimum of 1 by ImGui anyways, so oops.
                    float thickness = float.Min(1f, VisualSet.Editor_PreviewScaleMult);

                    // Draw a square outline for the tile sprite.
                    ImGui.GetWindowDrawList().AddRect(
                        posToOverlap, 
                        posToOverlap + visualSizeScaled,
                        gridColorPacked, 
                        0.0f, 
                        ImDrawFlags.None, 
                        thickness
                    );
                }

                ImGui.SetCursorScreenPos(posToOverlap);

                ImGui.SetNextItemSelectionUserData(i); // needed for MultiSelect
                ImGui.PushID(i);
                // FIXME: Make selectable invisible (remove background col and border highlight)!
                // FIXME: Crashes when using empty u8 string!
                ImGui.Selectable("", wasSelected, ImGuiSelectableFlags.None, visualSizeScaled);
                ImGui.PopID();

                if (ImGui.IsItemHovered())
                {
                    HoveredVisualInSet = posInVisualSet;

                    // Right-clicking on a sprite opens a menu to replace the sprite for any other "Tile"-named sprite
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.OpenPopup("##SelectTileSprite");
                        VisualInSetToModify = posInVisualSet;
                    }
                }

                if (((i + 1) % VisualSet.NumColumns) != 0)
                {
                    ImGui.SameLine();
                }
            }

            ImGui.PopStyleVar(numStyleVars);

            multiSelectIO = ImGui.EndMultiSelect();
            MaybeChangeActiveSet(multiSelectIO);
            SelectedToPaint.HandleMultiSelectRequests(multiSelectIO, VisualSet);
        }

        if (VisualInSetToModify != null)
        {
            // TODO:
            //DrawTileSpriteReplacementsPopup(tileLayer, World);
        }
        if (HoveredVisualInSet != null)
        {
            // FIXME: Set to null if no longer hovering anything?
        }
        ImGui.EndChild();
        
        if (VisualSet.Editor_CanAddOrRemoveVisuals())
        {
            bool noSelectedImages = SelectedToPaint.Selected.Count <= 0;
            if (noSelectedImages)
            {
                ImGui.BeginDisabled();
            }
            if (ImGui.Button("Delete Selected"))
            {
                // TODO
                // FIXME: Undo/Redo support!
                /*for (int nthImageToPaint = SelectedToPaint.Selected.Count - 1; nthImageToPaint >= 0; --nthImageToPaint)
                {
                    var (layerImageID, isNotFiller) = SelectedToPaint.Selected[nthImageToPaint];
                    if (isNotFiller)
                    {
                        tileLayer.DeleteLayerImage(layerImageID, World);
                    }
                }*/

                SelectedToPaint.ClearSelections();
            }
            if (noSelectedImages)
            {
                ImGui.EndDisabled();
            }
        }

        var visualColor = Color.White.ToVector4();
        var firstValidLayerImageID = SelectedToPaint.FirstValidPositionInVisualSet;
        var noValidSelectedImages = firstValidLayerImageID == null;
        if (noValidSelectedImages)
        {
            ImGui.BeginDisabled();
        }
        else
        {
            visualColor = tileLayer.Images[firstValidLayerImageID.Value].Item2.ToVector4();
        }
        if (ImGui.ColorEdit4("Change Selected ColorBlend", ref visualColor))
        {
            if (SelectedToPaint != null)
            {
                foreach (var (layerImageID, isNotFiller) in SelectedToPaint.Selected)
                {
                    if (isNotFiller)
                    {
                        tileLayer.ChangeImageColorBlend(
                            new Editor_LayerImageID(layerImageID),
                            new Color(visualColor), World
                        );
                    }
                }
            }
        }
        if (noValidSelectedImages)
        {
            ImGui.EndDisabled();
        }
    }

    // Shows all visual sets of a given type in a list.
    // TODO: If one is hovered over, a preview is shown.
    public static void ShowVisualSetSelectionByType(VisualSet.Editor_Types type)
    {
        // TODO: User starts off by selecting the visual set type (TileSet, ImageSet, etc.), to filter.
    }

    public void ShowImGuiButtonForVisual(PositionInVisualSet posInVisualSet, VisualSetVariantID variantID)
    {
        
    }
}


public class SelectedVisualsToPaint
{
    // NOTE: This is a flat 2D array. See NumColumns for the column count.
    // Some may be filler for a non-square selection scheme.
    public List<(PositionInVisualSet, bool IsNotFiller)> Selected { get; private set; } = new();
    public int NumColumns { get; private set; } = -1;

    public PositionInVisualSet? FirstValidPositionInVisualSet
    {
        get
        {
            foreach (var (posInSet, isNotFiller) in Selected)
            {
                if (isNotFiller)
                {
                    return posInSet;
                }
            }
            return null;
        }
    }

    public void ClearSelections()
    {
        Selected.Clear();
        NumColumns = -1;
    }

    public void HandleMultiSelectRequests(ImGuiMultiSelectIOPtr multiSelectIO, VisualSet visualSet)
    {
        for (int requestNum = 0; requestNum < multiSelectIO.Requests.Size; ++requestNum)
        {
            var request = multiSelectIO.Requests[requestNum];

            if (request.Type == ImGuiSelectionRequestType.SetAll)
            {
                if (request.Selected != 0) // Select all
                {
                    Selected.Clear();

                    for (ushort row = 0; row < visualSet.NumRows; ++row)
                    {
                        for (ushort col = 0; col < visualSet.NumColumns; ++col)
                        {
                            var posInVisualSet = new PositionInVisualSet(col, row);
                            Selected.Add((posInVisualSet, true));
                        }
                    }
                }
                else // Unselect all
                {
                    ClearSelections();
                }
            }
            else if (request.Type == ImGuiSelectionRequestType.SetRange)
            {
                for (var id = (ushort)request.RangeFirstItem; id <= (ushort)request.RangeLastItem; ++id)
                {
                    var col = (ushort)(id % visualSet.NumColumns);
                    var row = (ushort)(id / visualSet.NumColumns);
                    var posInVisualSet = new PositionInVisualSet(col, row);

                    if (request.Selected == 0) // selection removed
                    {
                        UpdateMultiImagePaintSelection(null, posInVisualSet);
                    }
                    else // selection added
                    {
                        if (Selected.Count == 0)
                        {
                            NumColumns = 1;
                            Selected.Add((posInVisualSet, true));
                        }
                        else
                        {
                            UpdateMultiImagePaintSelection(posInVisualSet, null);
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

    private void UpdateMultiImagePaintSelection(
        PositionInVisualSet? toAddPos = null, 
        PositionInVisualSet? toRemovePos = null)
    {
        // LayerImageIDs may be invalid here, for odd selection schemes.
        // Ex: picking 2 sprites that are diagonal from each other.
        // This would produce a 2x2 selection scheme, with 2 tiles being 'invalid' (empty).
        List<PositionInVisualSet> validPositions = new();
        foreach (var (visualPosInSet, isNotFiller) in Selected)
        {
            if (isNotFiller)
            {
                validPositions.Add(visualPosInSet);
            }
        }
        Selected.Clear();
        NumColumns = -1;

        if (toAddPos.HasValue)
        {
            validPositions.Add(toAddPos.Value);
        }
        if (toRemovePos.HasValue)
        {
            validPositions.Remove(toRemovePos.Value);
        }

        if (validPositions.Count == 0)
        {
            return;
        }

        var top = ushort.MaxValue;
        var bottom = ushort.MinValue;
        var left = ushort.MaxValue;
        var right = ushort.MinValue;

        foreach (var visualPosInSet in validPositions)
        {
            var col = visualPosInSet.X;
            var row = visualPosInSet.Y;

            top = ushort.Min(top, row);
            bottom = ushort.Max(bottom, row);
            left = ushort.Min(left, col);
            right = ushort.Max(right, col);
        }

        NumColumns = right - left + 1;

        for (ushort row = top; row <= bottom; ++row)
        {
            for (ushort col = left; col <= right; ++col)
            {
                var currentPos = new PositionInVisualSet(col, row);
                if (validPositions.Contains(currentPos))
                {
                    Selected.Add((currentPos, true));
                }
                else
                {
                    Selected.Add((currentPos, false));
                }
            }
        }
    }
}

#endif