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

namespace RollAndCash.Editor;

public class PaintingSelection
{
    /// <summary>
    /// NOTE: This is a flat 2D array. See NumColumns for the column count. <br/>
    /// Some entries may be filler for an incomplete square selection scheme. <br/>
    /// </summary>
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