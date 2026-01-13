#if DEBUG
using System;
using System.Numerics;
using MoonWorks.Graphics;
using MoonWorks.Math;
using RollAndCash.Utility;

using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.SDL3;
using ImSDLEvent = Hexa.NET.ImGui.Backends.SDL3.SDLEvent;
using ImSDLWindow = Hexa.NET.ImGui.Backends.SDL3.SDLWindow;
using ImSDLGPUDevice = Hexa.NET.ImGui.Backends.SDL3.SDLGPUDevice;
using ImSDLGPUCommandBuffer = Hexa.NET.ImGui.Backends.SDL3.SDLGPUCommandBuffer;
using ImSDLGPURenderPass = Hexa.NET.ImGui.Backends.SDL3.SDLGPURenderPass;

// Using partial here, because we need to use Rendering.SpriteInstanceData,
// which may not be part of every ImGui-using app we make.
public static partial class ImGuiExtensions
{
    /// <summary>
    /// Only the sprite is rotated, not the background.
    /// Depth information is discarded.
    /// The background does scale, though.
    /// Mostly useful for rotations where the image still fits in the background.
    /// </summary>
    public static void SpriteWithBgAndOutline(
        Texture texture,
        Rendering.SpriteInstanceData spriteInstanceData,
        Vector4 bgCol,
        Vector4 outlineCol,
        ImGuiSamplerType samplerType = ImGuiSamplerType.PointClamp
    )
    {
        var drawList = ImGui.GetWindowDrawList();

        // FIXME: Do I need to make positions non-relative?
        var screenPos = ImGui.GetCursorScreenPos();
        var imageSize = Vector2.Abs(spriteInstanceData.Scale);

        // Draw background.
        {
            var rectTopLeft = screenPos;
            var rectBottomRight = screenPos + imageSize;

            drawList.AddRectFilled(
                rectTopLeft,
                rectBottomRight,
                ImGui.GetColorU32(bgCol)
            );
        }

        // Scale might have negative values, to represent a flip, 
        // so we tiptoe around that.
        var centerOfSpriteInScreenSpacePos = screenPos + (imageSize * 0.5f);
        var imguiSpriteInfo = spriteInstanceData.ToImGuiRenderInfo(
            centerOfSpriteInScreenSpacePos
        );

        // Draw rotated image.
        // FIXME: Crashes!!!!
        SetCustomImGuiSampler(samplerType);
        drawList.AddImageQuad(
            GetTextureRef(texture),
            
            imguiSpriteInfo.Pos1, 
            imguiSpriteInfo.Pos2, 
            imguiSpriteInfo.Pos3, 
            imguiSpriteInfo.Pos4,

            imguiSpriteInfo.UV1, 
            imguiSpriteInfo.UV2, 
            imguiSpriteInfo.UV3, 
            imguiSpriteInfo.UV4,

            imguiSpriteInfo.Color
        );
        ResetImGuiSampler();

        // Reduce the thickness when zooming out. 
        // FIXME: Probably gets set to a minimum of 1 by ImGui anyways, so oops.
        //float thickness = float.Min(1f, VisualSet.Editor_PreviewScaleMult);
        float thickness = 1f;

        // Draw a square outline for the tile sprite.
        ImGui.GetWindowDrawList().AddRect(
            screenPos, 
            screenPos + imageSize,
            ImGui.GetColorU32(outlineCol), 
            0.0f, 
            ImDrawFlags.None, 
            thickness
        );
    }
}
#endif