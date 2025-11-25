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

public class TileLayerMenu
{
    unsafe ImGuiTextFilterPtr TileSetSearchFilter = new(ImGui.ImGuiTextFilter(""u8));
    void DrawTileSetSelectionPopup(LiveLevel.EditorLayer levelLayer)
    {
        if (ImGui.BeginPopup("##SetTileset"))
        {
            ImGui.Text("Select TileSet");
            ImGui.Separator();
            TileSetSearchFilter.Draw("Search");

            foreach (var tileSetName in TileSets.Names)
            {
                // TODO!!!
            }
        }
    }

    public void Show(LiveLevel.EditorLayer tileLayer, World World)
    {
        var colorBlendVec = tileLayer.Color.ToVector4();
        if (ImGui.ColorEdit4("Layer Color Blend", ref colorBlendVec))
        {
            tileLayer.ChangeLayerColorBlend(new Color(colorBlendVec), World);
        }

        // FIXME: Undo/Redo support!
        if (ImGui.Button("Set TileSet"))
        {
            ImGui.OpenPopup("##SetTileset");
        }
        DrawTileSetSelectionPopup(tileLayer);
    }
}
#endif