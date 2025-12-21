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
using RollAndCash.Rendering;
using RollAndCash.Systems;

namespace RollAndCash.Editor;

// Visual sets, like TileSets and ImageSets, are displayed with this class.
// It also handles selecting parts of the sets to paint them in-level later.
public class VisualSetMenu
{
    //== Global state

    // Only a single VisualSet can make paint selections at any given time.
    // If another becomes active, then it overrides the previous selections.
    public static VisualSetMenu ActiveVisualSetMenu { get; private set; } = null;

    //== Constants
    public readonly VisualSet VisualSet;

    //== Visual constants
    static readonly Color SelectedOutlineColor = Color.Chocolate;
    static readonly Color HoveredOutlineColor = Color.White;
    static readonly Color ModifyingOutlineColor = Color.Red;
    //private const float MenuBottomPortionWidth = 1;

    //== State
    public VisualSetVariantID CurrentVariantID;
    public static PaintingSelection SelectedToPaint = new();
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
    public bool Show(
        World world, 
        PrefabManipulator prefabManipulator, 
        RenderingManipulator renderingManipulator)
    {
        bool menuStaysOpen = true;
        if (!ImGui.Begin(VisualSet.Name + $"##{VisualSet.Editor_Type}", ref menuStaysOpen)
            || !menuStaysOpen)
        {
            SelectedToPaint.ClearSelections();
            ImGui.End();
            return menuStaysOpen;            
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
        ShowVisualSelection(menuBottomPortionWidth, world, prefabManipulator, renderingManipulator);

        ImGui.End();
        return menuStaysOpen;
    }
    
    private void MaybeChangeActiveSet(ImGuiMultiSelectIOPtr multiSelectIO)
    {
        if (multiSelectIO.Requests.Size != 0
            && ActiveVisualSetMenu != this)
        {
            ActiveVisualSetMenu = this;

            SelectedToPaint.ClearSelections();
            HoveredVisualInSet = null;
            VisualInSetToModify = null;
        }
    }

    private void ShowVisualSelection(
        float menuBottomPortionWidth, 
        World world, 
        PrefabManipulator prefabManipulator,
        RenderingManipulator renderingManipulator
        )
    {
        var scalingFactor = ImGui.GetWindowViewport().Size / Dimensions.GAME_DIMENSIONS 
            * VisualSet.Editor_PreviewScaleMult
        ;

        // Show every selected Visual from the VisualSet
        if (ImGui.BeginChild("##ViewVisualsFromSet"u8, new Vector2(-1, -menuBottomPortionWidth),
            ImGuiChildFlags.AlwaysAutoResize | ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY,
            ImGuiWindowFlags.AlwaysHorizontalScrollbar | ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            // Multi-selection code based off of Dear Imgui's Example Assets Browser: 
            // https://github.com/ocornut/imgui/blob/2ab3946ecb12962eff96c9bc13ef83d403c84dd8/imgui_demo.cpp#L10538
            var multiSelectIO = ImGui.BeginMultiSelect(
                ImGuiMultiSelectFlags.BoxSelect2D // enable drag-selection
                | ImGuiMultiSelectFlags.ClearOnEscape
                | ImGuiMultiSelectFlags.ClearOnClickVoid
                | ImGuiMultiSelectFlags.NavWrapX, // Enable keyboard wrapping on X axis
                SelectedToPaint.NumTrueSelections,
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

                // FIXME: Allow sprite animations to play (simulate frame countdown?)
                var visualFromSetID = new VisualFromSetID_ForSpawning(
                    posInVisualSet,
                    VisualSet.ID,
                    CurrentVariantID
                );
                var maybeDummyEntity = VisualSet.Editor_TryCreateEntityFromVisualSet(
                    visualFromSetID,
                    default, // spawn pos doesn't matter
                    world,
                    prefabManipulator,
                    null,
                    null,
                    true,
                    false
                );
                if (!maybeDummyEntity.HasValue)
                {
                    // FIXME: Display a warning texture.
                    Logger.LogError($"Failed to spawn dummy entity for visual from a visual set: {posInVisualSet}");
                    continue;
                }
                world.Set(maybeDummyEntity.Value, 
                    new VisualScale(scalingFactor 
                        * (world.Has<VisualScale>(maybeDummyEntity.Value) ? 
                            world.Get<VisualScale>(maybeDummyEntity.Value).Scale : Vector2.One
                        )
                    )
                );
                var (spriteRenderData, texture) = 
                    renderingManipulator.Editor_GetSpriteInstanceDataAndTexture(
                        maybeDummyEntity.Value
                    )
                ;
                world.Destroy(maybeDummyEntity.Value);
                maybeDummyEntity = null;

                if (texture == null)
                {
                    // Error should already be logged.
                    continue;
                }

                //var visualSizeScaled = VisualSet.GetVisualSize(posInVisualSet, CurrentVariantID) * scalingFactor;
                var visualSizeScaled = spriteRenderData.Scale;

                bool wasSelected = SelectedToPaint.TrueSelections.Contains(posInVisualSet);
                bool wasHovered = posInVisualSet == HoveredVisualInSet;
                bool isReplacing = posInVisualSet == VisualInSetToModify;

                // Draw sprite grid outline.
                var gridOutlineColor = Color.Transparent;
                if (VisualSet.Editor_ShowGrid || wasSelected || wasHovered || isReplacing)
                {
                    if (isReplacing)
                    {
                        gridOutlineColor = ModifyingOutlineColor;
                    }
                    else if (wasHovered)
                    {
                        var hoveredColor = HoveredOutlineColor;
                        if (wasSelected)
                        {
                            // Make it clear if a hovered tile is selected or not.
                            hoveredColor = Color.Lerp(hoveredColor, SelectedOutlineColor, 0.5f);
                        }
                        gridOutlineColor = hoveredColor;
                    }
                    else if (wasSelected)
                    {
                        gridOutlineColor = SelectedOutlineColor;
                    }
                    else if (VisualSet.Editor_ShowGrid)
                    {
                        gridOutlineColor = new Color(EditorSystem.GridLineColor);
                    }
                    else
                    {
                        Logger.LogError("Unhandled case!");
                        continue;
                    }
                }

                Vector2 posToOverlap = ImGui.GetCursorScreenPos();
                //var origin = sprite.Origin * scalingFactor;
                //var offset = -origin - new Vector2(currentFrame.FrameRect.X, currentFrame.FrameRect.Y) * scalingFactor;
                // FIXME: How to handle non-tile sprites w/ unique offsets?
                //ImGui.SetCursorScreenPos(posToOverlap + offset + (visualSizeScaled * 0.5f));
                //ImGui.SetNextItemAllowOverlap(); // FIXME: Do we even need this still?

                // Draw sprite
                ImGuiExtensions.SpriteWithBgAndOutline(
                    texture,
                    spriteRenderData,
                    Color.Transparent.ToVector4(),
                    gridOutlineColor.ToVector4(),
                    ImGuiBackend.SamplerType.PointClamp
                );

                ImGui.SetCursorScreenPos(posToOverlap);

                ImGui.SetNextItemSelectionUserData(i); // needed for MultiSelect
                ImGui.PushID(i);
                // FIXME: Make selectable invisible (remove background col and border highlight)!
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

        bool visualSetWasChanged = false;
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
                visualSetWasChanged = true;

                // TODO: Implement!
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

        // FIXME: Don't allow changing ColorBlend for visuals for fully transparent visuals!
        var cantChangeVisualColor = SelectedToPaint.Selected.Count == 0
            || !VisualSet.CanSetVisualColor(CurrentVariantID)
        ;
        if (cantChangeVisualColor)
        {
            ImGui.BeginDisabled();
        }
        else
        {
            // If there's only 1 selected visual, or if they all have the same ColorBlend, preview that color.
            // Else, show default color.
            if (SelectedToPaint.Selected.Count >= 1)
            {
                var maybeFirstValidPosition = SelectedToPaint.FirstValidPositionInVisualSet;
                if (maybeFirstValidPosition.HasValue)
                {
                    Color currentColor = VisualSet.GetVisualColor(maybeFirstValidPosition.Value, CurrentVariantID);
                    bool allSameColor = true;

                    int i = 1; 
                    while (i < SelectedToPaint.Selected.Count)
                    {
                        var (posInVisualSet, isNotFiller) = SelectedToPaint.Selected[i];
                        if (isNotFiller)
                        {
                            if (VisualSet.GetVisualColor(posInVisualSet, CurrentVariantID) != currentColor)
                            {
                                allSameColor = false;
                                break;
                            }
                        }
                        ++i;
                    }

                    if (allSameColor)
                    {
                        visualColor = currentColor.ToVector4();
                    }
                }
                else
                {
                    Logger.LogError("There should be a valid position here!");
                    visualColor = Color.Red.ToVector4();
                }
            }
        }
        if (ImGui.ColorEdit4("Change Selected ColorBlend", ref visualColor))
        {
            visualSetWasChanged = true;

            // Apply the changes.
            foreach (var (posInVisualSet, isNotFiller) in SelectedToPaint.Selected)
            {
                if (isNotFiller)
                {
                    VisualSet.Editor_TrySetVisualColor(posInVisualSet, CurrentVariantID, new Color(visualColor));
                    // WARNING: Already-loaded entities won't be modified until they're reloaded!
                    // The reason I won't reload them automatically is in case some have unsaved, unique changes.
                    // I definitely don't want to auto-save for each change either.
                    // TODO: Find a better solution than asking for a reload?
                }
            }
        }
        if (cantChangeVisualColor)
        {
            ImGui.EndDisabled();
        }

        if (visualSetWasChanged)
        {
            ImGui.OpenPopup("VisualSetChangedWarning"u8);
        }
        if (ImGui.BeginPopup("VisualSetChangedWarning"u8))
        {
            ImGui.TextColored(Color.PaleVioletRed.ToVector4(), "Already-loaded entities will need to be reloaded for the changes to appear!"u8);
            ImGui.Button("[OK]"u8);
            ImGui.EndPopup();
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

#endif