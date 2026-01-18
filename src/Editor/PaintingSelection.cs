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
using RollAndCash.Systems;

namespace RollAndCash.Editor;

public class PaintingSelection
{
    /// <summary>
    /// NOTE: This is a flat 2D array. See NumColumns for the column count. <br/>
    /// Some entries may be filler for an incomplete square selection scheme. <br/>
    /// </summary>
    public List<(PositionInVisualSet, bool IsNotFiller)> Selected 
        { get; private set; } = new();
    public List<PositionInVisualSet> TrueSelections 
        { get; private set; } = new();
    public int NumColumns { get; private set; } = -1;
    public int NumTrueSelections => TrueSelections.Count;

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
        TrueSelections.Clear();
        NumColumns = -1;
    }

    public void HandleMultiSelectRequests(
        ImGuiMultiSelectIOPtr multiSelectIO, 
        VisualSet visualSet
        )
    {
        for (int requestNum = 0; 
            requestNum < multiSelectIO.Requests.Size; 
            ++requestNum)
        {
            var request = multiSelectIO.Requests[requestNum];

            if (request.Type == ImGuiSelectionRequestType.SetAll)
            {
                ClearSelections();
                if (request.Selected != 0) // Select all
                {
                    for (ushort row = 0; row < visualSet.NumRows; ++row)
                    {
                        for (ushort col = 0; col < visualSet.NumColumns; ++col)
                        {
                            var posInVisualSet = new PositionInVisualSet(col, row);
                            TrueSelections.Add(posInVisualSet);
                        }
                    }
                }
            }
            else if (request.Type == ImGuiSelectionRequestType.SetRange)
            {
                for (var id = (ushort)request.RangeFirstItem; 
                    id <= (ushort)request.RangeLastItem; 
                    ++id)
                {
                    var col = (ushort)(id % visualSet.NumColumns);
                    var row = (ushort)(id / visualSet.NumColumns);
                    var posInVisualSet = new PositionInVisualSet(col, row);

                    if (request.Selected == 0) // selection removed
                    {
                        TrueSelections.Remove(posInVisualSet);
                    }
                    else // selection added
                    {
                        TrueSelections.Add(posInVisualSet);
                    }
                }
            }
            else
            {
                throw new NotImplementedException("Unexpected selection request type!");
            }
        }

        if (multiSelectIO.Requests.Size != 0)
        {
            UpdateMultiImagePaintSelection();
        }
    }

    private void UpdateMultiImagePaintSelection()
    {
        // Entries in Selected may be filler here, for odd selection schemes.
        // Ex: picking 2 sprites that are diagonal from each other.
        // This would produce a 2x2 selection scheme, with 2 tiles being 'invalid' (empty).
        Selected.Clear();
        NumColumns = -1;
        
        if (TrueSelections.Count == 0)
        {
            return;
        }

        // Add selections to flat 2d array, 
        // to easily handle tile spacing w/ potential gaps.

        var top = ushort.MaxValue;
        var bottom = ushort.MinValue;
        var left = ushort.MaxValue;
        var right = ushort.MinValue;

        foreach (var visualPosInSet in TrueSelections)
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
                if (TrueSelections.Contains(currentPos))
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