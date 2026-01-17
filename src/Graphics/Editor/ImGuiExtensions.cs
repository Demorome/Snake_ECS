using System;
using System.Numerics;
using MoonWorks.Graphics;
using MoonWorks.Math;
using RollAndCash.Utility;

using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.SDL3;

public enum ImGuiSnapPosition
{
    Custom = -1,
    Center = -2,

    Top_Left = 0,
    Top_Right,
    Bottom_Left,
    Bottom_Right
}

// Credits to @darkerbit for Image-drawing extension methods: 
// https://gist.github.com/darkerbit/6bfb661d7ce9263ddd7dcc7b475460e0
// Tweaked to use the Hexa.NET SDL3 backend.
/// <summary>
/// ImGui extension methods.
/// </summary>
public static partial class ImGuiExt
{
    public const float TransparentWindowBgAlpha = 0.35f;

    public static string SnapPosToString(ImGuiSnapPosition s)
        => s switch
    {
        ImGuiSnapPosition.Custom => "Custom",
        ImGuiSnapPosition.Center => "Center",

        ImGuiSnapPosition.Bottom_Left => "Bottom-Left",
        ImGuiSnapPosition.Bottom_Right => "Bottom-Right",
        ImGuiSnapPosition.Top_Left => "Top-Left",
        ImGuiSnapPosition.Top_Right => "Top-Right",

        _ => throw new ArgumentException("Bad input!")
    };

    // Code taken from imgui_demo's ShowExampleAppSimpleOverlay.
    /// <summary>
    /// </summary>
    /// <returns>True if window is snapped (can't move), false otherwise.</returns>
    private static bool MaybeSetWindowSnapPos(ImGuiSnapPosition location)
    {
        var snappedLocked = false;
        if (location >= 0)
        {
            const float PAD = 10.0f;
            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            Vector2 work_pos = viewport.WorkPos; // Use work area to avoid menu-bar/task-bar, if any!
            Vector2 work_size = viewport.WorkSize;

            Vector2 window_pos;
            window_pos.X 
                = (((int)location & 1) != 0) 
                ? (work_pos.X + work_size.X - PAD) 
                : (work_pos.X + PAD);
            window_pos.Y 
                = (((int)location & 2) != 0) 
                ? (work_pos.Y + work_size.Y - PAD) 
                : (work_pos.Y + PAD);

            Vector2 window_pos_pivot;
            window_pos_pivot.X 
                = (((int)location & 1) != 0) 
                ? 1.0f
                : 0.0f;
            window_pos_pivot.Y = 
                (((int)location & 2) != 0) 
                ? 1.0f
                : 0.0f;

            ImGui.SetNextWindowPos(
                window_pos, 
                ImGuiCond.Always, 
                window_pos_pivot
            );
            ImGui.SetNextWindowViewport(viewport.ID);
            snappedLocked = true;
        }
        else if (location == ImGuiSnapPosition.Center)
        {
            // Center window
            ImGui.SetNextWindowPos(
                ImGui.GetMainViewport().Size / 2, 
                ImGuiCond.Always, 
                new Vector2(0.5f, 0.5f)
            );
            snappedLocked = true;
        }
        return snappedLocked;
    }

    public static ImGuiWindowFlags DoLocationSnappedOverlayWindowSetup(
        ImGuiSnapPosition snapPosition
    )
    {
        // Transparent background.
        ImGui.SetNextWindowBgAlpha(TransparentWindowBgAlpha);

        var windowFlags 
            = ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoDocking
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoNav
            | ImGuiWindowFlags.NoSavedSettings;

        if (MaybeSetWindowSnapPos(snapPosition))
        {
            windowFlags |= ImGuiWindowFlags.NoMove;
        }

        return windowFlags;
    }

    public static void ShowCloseOrCollapseWindowPopup(ref bool pOpen)
    {
        if (ImGui.BeginPopupContextWindow())
        {
            if (pOpen && ImGui.MenuItem("Close"u8))
            {
                pOpen = false;
            }

            var isCollapsed = ImGui.IsWindowCollapsed();
            if (ImGui.MenuItem("Collapse"u8, isCollapsed))
            {
                ImGui.SetWindowCollapsed(!isCollapsed);
            }

            ImGui.EndPopup();
        }
    }

    public static void ShowChangePositionPopup(
        ref bool pOpen,
        ref ImGuiSnapPosition location
        )
    {
        if (ImGui.BeginPopupContextWindow())
        {
            foreach (var nthLocation in Enum.GetValues<ImGuiSnapPosition>())
            {
                if (ImGui.MenuItem(
                    SnapPosToString(nthLocation),
                    location == nthLocation)) 
                {
                    location = nthLocation;
                }
            }

            if (pOpen && ImGui.MenuItem("Close"))
            {
                pOpen = false;
            }

            ImGui.EndPopup();
        }
    }

    public static ImGuiWindowFlags DoMoveableOverlayWindowSetup()
    {
        // Transparent background.
        ImGui.SetNextWindowBgAlpha(TransparentWindowBgAlpha);

        return ImGuiWindowFlags.NoDecoration
                | ImGuiWindowFlags.NoDocking
                | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav;
    }

    // FIXME: Replace when Hexa ImGui includes this struct in an update!
    unsafe struct ImGui_ImplSDLGPU3_RenderState
    {
        public SDLGPUDevice*      Device;

        // Default sampler (bilinear filtering)
        public void*     SamplerDefault;

        // Current sampler (may be changed by callback)
        public void*     SamplerCurrent;
    };

    // According to this, we can no longer set sampler bindings per-texture:
    // https://github.com/ocornut/imgui/wiki/Image-Loading-and-Displaying-Examples#example-for-sdl_gpu-users
    // https://github.com/ocornut/imgui/blob/f64c7c37efd9b0cead78e84f9378398b68ae5f61/backends/imgui_impl_sdlgpu3.cpp#L28
    // "If you need to change the current sampler,
    // you can access the ImGui_ImplSDLGPU3_RenderState struct."
    // Modifying Render State:
    // https://github.com/ocornut/imgui/wiki/Image-Loading-and-Displaying-Examples/8682f7ad59f260e8f8d7ca02c164a24403225111#modifying-render-state
    /// <summary>
    /// For SDL_GPU backend: Callback to modify current sampler.
    /// FIXME: Re-enable when this is fixed upstream in Hexa.ImGui!
    /// </summary>
    private static unsafe void ImDrawCallback_SetSampler(
        ImDrawList* parent_list, 
        ImDrawCmd* cmd
        )
    {
        /*
        var state = (ImGui_ImplSDLGPU3_RenderState*)
            ImGui.GetPlatformIO()
            .RendererRenderState;

        void* sampler = cmd->UserCallbackData == null
            ? cmd->UserCallbackData 
            : state->SamplerDefault;

        state->SamplerCurrent = sampler;*/
    }

    private static unsafe void SetCustomImGuiSampler(
        Sampler sampler
    )
    {
        ImGui.GetWindowDrawList().AddCallback(
            ImDrawCallback_SetSampler, 
            (void*)sampler.Handle
        );
    }

    private static void SetCustomImGuiSampler(
        ImGuiSamplerType samplerType
    )
    {
        var backend = ImGuiBackend.Instance!;
        var sampler = backend.SamplerForType(samplerType);
        SetCustomImGuiSampler(sampler);
    }

    private static unsafe void ResetImGuiSampler()
    {
        ImGui.GetWindowDrawList().AddCallback(
            ImDrawCallback_SetSampler, null
        );
    }

    public static unsafe ImTextureRef GetTextureRef(nint texID)
    {
        return new ImTextureRef(null, texID);
    }

    public static void Image(
        Texture texture, 
        Vector2 imageSize,
        ImGuiSamplerType samplerType = ImGuiSamplerType.LinearClamp
        )
    {
        SetCustomImGuiSampler(samplerType);

        ImGui.Image(
            GetTextureRef(texture),
            imageSize
        );

        ResetImGuiSampler();
    }

    public static void Image(
        Texture texture,
        Vector2 imageSize,
        Vector2 uv0,
        ImGuiSamplerType samplerType = ImGuiSamplerType.LinearClamp
    )
    {
        SetCustomImGuiSampler(samplerType);

        ImGui.Image(
            GetTextureRef(texture),
            imageSize,
            uv0
        );

        ResetImGuiSampler();
    }

    public static void Image(
        Texture texture,
        Vector2 imageSize,
        Vector2 uv0,
        Vector2 uv1,
        ImGuiSamplerType samplerType = ImGuiSamplerType.LinearClamp
    )
    {
        SetCustomImGuiSampler(samplerType);

        ImGui.Image(
            GetTextureRef(texture),
            imageSize,
            uv0,
            uv1
        );

        ResetImGuiSampler();
    }

    // Q: Why aren't there 'tintColor' and 'borderColor' overloads for Image() anymore?
    // A: Dear ImGui update: "removed 'tint_col', 'border_col' parameters from Image()"
    // Recommended instead to use ImGuiCol_Border color + style.ImageBorderSize / ImGuiStyleVar_ImageBorderSize.
    // Added ImageWithBg() function which has both 'bg_col' (which was missing) and 'tint_col'.
    // https://github.com/ocornut/imgui/commit/494ea57b65325f00165da10e6b57b4f295a65bca
    public static void ImageWithBg(
        Texture texture,
        Vector2 imageSize,
        Vector2 uv0,
        Vector2 uv1,
        Vector4 bgCol,
        ImGuiSamplerType samplerType = ImGuiSamplerType.LinearClamp
    )
    {
        SetCustomImGuiSampler(samplerType);

        ImGui.ImageWithBg(
            GetTextureRef(texture),
            imageSize,
            uv0,
            uv1,
            bgCol
        );

        ResetImGuiSampler();
    }

    public static void ImageWithBg(
        Texture texture,
        Vector2 imageSize,
        Vector2 uv0,
        Vector2 uv1,
        Vector4 bgCol,
        Vector4 tintCol,
        ImGuiSamplerType samplerType = ImGuiSamplerType.LinearClamp
    )
    {
        SetCustomImGuiSampler(samplerType);

        ImGui.ImageWithBg(
            GetTextureRef(texture),
            imageSize,
            uv0,
            uv1,
            bgCol,
            tintCol
        );

        ResetImGuiSampler();
    }

    public static bool ImageButton(
        string id,
        Texture texture,
        Vector2 imageSize,
        ImGuiSamplerType samplerType = ImGuiSamplerType.LinearClamp
    )
    {
        SetCustomImGuiSampler(samplerType);

        var result = ImGui.ImageButton(
            id,
            GetTextureRef(texture),
            imageSize
        );

        ResetImGuiSampler();

        return result;
    }

    public static bool ImageButton(
        string id,
        Texture texture,
        Vector2 imageSize,
        Vector2 uv0,
        ImGuiSamplerType samplerType = ImGuiSamplerType.LinearClamp
    )
    {
        SetCustomImGuiSampler(samplerType);

        var result = ImGui.ImageButton(
            id,
            GetTextureRef(texture),
            imageSize,
            uv0
        );

        ResetImGuiSampler();

        return result;
    }

    public static bool ImageButton(
        string id,
        Texture texture,
        Vector2 imageSize,
        Vector2 uv0,
        Vector2 uv1,
        ImGuiSamplerType samplerType = ImGuiSamplerType.LinearClamp
    )
    {
        SetCustomImGuiSampler(samplerType);

        var result = ImGui.ImageButton(
            id,
            GetTextureRef(texture),
            imageSize,
            uv0,
            uv1
        );

        ResetImGuiSampler();

        return result;
    }

    public static bool ImageButton(
        string id,
        Texture texture,
        Vector2 imageSize,
        Vector2 uv0,
        Vector2 uv1,
        Vector4 bgCol,
        ImGuiSamplerType samplerType = ImGuiSamplerType.LinearClamp
    )
    {
        SetCustomImGuiSampler(samplerType);

        var result = ImGui.ImageButton(
            id,
            GetTextureRef(texture),
            imageSize,
            uv0,
            uv1,
            bgCol
        );

        ResetImGuiSampler();

        return result;
    }

    public static bool ImageButton(
        string id,
        Texture texture,
        Vector2 imageSize,
        Vector2 uv0,
        Vector2 uv1,
        Vector4 bgCol,
        Vector4 tintCol,
        ImGuiSamplerType samplerType = ImGuiSamplerType.LinearClamp
    )
    {
        SetCustomImGuiSampler(samplerType);

        var result = ImGui.ImageButton(
            id,
            GetTextureRef(texture),
            imageSize,
            uv0,
            uv1,
            bgCol,
            tintCol
        );

        ResetImGuiSampler();

        return result;
    }
}