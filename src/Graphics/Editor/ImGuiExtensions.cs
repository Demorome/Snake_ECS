using System;
using System.Numerics;
using MoonWorks.Graphics;
using MoonWorks.Math;
using RollAndCash.Utility;

using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.SDL3;

// Credits to @darkerbit: https://gist.github.com/darkerbit/6bfb661d7ce9263ddd7dcc7b475460e0
// Tweaked to use the Hexa.NET SDL3 backend.
// According to this, we can no longer set sampler bindings per-texture:
// https://github.com/ocornut/imgui/wiki/Image-Loading-and-Displaying-Examples#example-for-sdl_gpu-users
// https://github.com/ocornut/imgui/blob/f64c7c37efd9b0cead78e84f9378398b68ae5f61/backends/imgui_impl_sdlgpu3.cpp#L28
// "If you need to change the current sampler,
// you can access the ImGui_ImplSDLGPU3_RenderState struct."
// Modifying Render State:
// https://github.com/ocornut/imgui/wiki/Image-Loading-and-Displaying-Examples/8682f7ad59f260e8f8d7ca02c164a24403225111#modifying-render-state
public static partial class ImGuiExtensions
{
    // FIXME: Replace when Hexa ImGui includes this struct in an update!
    unsafe struct ImGui_ImplSDLGPU3_RenderState
    {
        public SDLGPUDevice*      Device;

        // Default sampler (bilinear filtering)
        public void*     SamplerDefault;

        // Current sampler (may be changed by callback)
        public void*     SamplerCurrent;
    };

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