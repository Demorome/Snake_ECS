using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using MoonWorks;
using MoonWorks.AsyncIO;
using MoonWorks.Graphics;
using MoonWorks.Input;
using SDL3;
using Buffer = MoonWorks.Graphics.Buffer;
using System.Diagnostics;

using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.SDL3;
using ImSDLEvent = Hexa.NET.ImGui.Backends.SDL3.SDLEvent;
using ImSDLWindow = Hexa.NET.ImGui.Backends.SDL3.SDLWindow;
using ImSDLGPUDevice = Hexa.NET.ImGui.Backends.SDL3.SDLGPUDevice;
using ImSDLGPUCommandBuffer = Hexa.NET.ImGui.Backends.SDL3.SDLGPUCommandBuffer;
using ImSDLGPURenderPass = Hexa.NET.ImGui.Backends.SDL3.SDLGPURenderPass;


public enum ImGuiSamplerType
{
    LinearClamp = 0,
    LinearWrap = 1,
    PointClamp = 2,
    PointWrap = 3,
}

// Credits to @darkerbit: https://gist.github.com/darkerbit/6bfb661d7ce9263ddd7dcc7b475460e0
// It's been almost completely changed to use Hexa's SDL_GPU ImGui backend.
public class ImGuiBackend : IDisposable
{
    public static ImGuiBackend? Instance { get; private set; }

    private Game Game { get; }

    private Sampler[] Samplers;

    public ImGuiBackend(Game game)
    {
        Instance = this;
        Game = game;

        Samplers =
        [
            Sampler.Create(Game.GraphicsDevice, "Dear ImGui Linear Clamp Sampler", SamplerCreateInfo.LinearClamp),
            Sampler.Create(Game.GraphicsDevice, "Dear ImGui Linear Wrap Sampler", SamplerCreateInfo.LinearWrap),
            Sampler.Create(Game.GraphicsDevice, "Dear ImGui Point Clamp Sampler", SamplerCreateInfo.PointClamp),
            Sampler.Create(Game.GraphicsDevice, "Dear ImGui Point Wrap Sampler", SamplerCreateInfo.PointWrap),
        ];

        InitImGuiRenderingDetails();

        game.OnProcessEvent += ProcessEvents;
    }

    /// <summary>
    /// Call only during draw step. <br/>
    /// To finish the frame, either call <see cref="UploadAndRenderBuffers"/>,
    /// or <see cref="EndFrame"/>.
    /// </summary>
    public static void NewFrame(/*TimeSpan delta*/)
    {
        // According to this, we don't need to update `io.DeltaTime`, etc.:
        // https://github.com/ocornut/imgui/blob/master/docs%2FBACKENDS.md
        ImGuiImplSDL3.SDLGPU3NewFrame();
        ImGuiImplSDL3.NewFrame();
        ImGui.NewFrame();
    }

    /// <summary>
    /// Use when you need to prepare draw info to live-debug Systems.
    /// </summary>
    public static void EndFrame()
    {
        ImGui.EndFrame();
    }

    /// <summary>
    /// Will perform the render pass using ColorTargetInfo. </br>
    /// Implicitly ends the frame.
    /// </summary>
    public static void UploadAndRenderBuffers(
        CommandBuffer commandBuffer,
        ColorTargetInfo colorTargetInfo
        )
    {
        // Prepare and upload buffers.
        ImGui.Render();
        var drawData = ImGui.GetDrawData();
        bool isMinimized = drawData.DisplaySize.X <= 0 
            || drawData.DisplaySize.Y <= 0;

        if (!isMinimized)
        {
            // Does a copy pass
            UploadImGuiBuffers(
                drawData,
                commandBuffer
            );

            var guiRenderPass = commandBuffer.BeginRenderPass(
                colorTargetInfo
            );

            RenderImGuiBuffers(
                drawData, 
                commandBuffer, 
                guiRenderPass
            );
            
            commandBuffer.EndRenderPass(guiRenderPass);
        }
    }

    /// <summary>
    /// Add in your main loop, after rendering your main viewport.
    /// </summary>
    public static void RenderOtherPlatformWindows()
    {
        var configFlags = ImGui.GetIO().ConfigFlags;

        // Update and Render additional Platform Windows.
        // https://github.com/ocornut/imgui/wiki/Multi-Viewports#how-can-i-enable-multi-viewports-
        if ((configFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            ImGui.UpdatePlatformWindows();
            ImGui.RenderPlatformWindowsDefault();
        }
    }

    private static unsafe void UploadImGuiBuffers(
        ImDrawDataPtr drawData,
        CommandBuffer commandBuffer
    )
    {
        ImGuiImplSDL3.SDLGPU3PrepareDrawData(
            drawData, 
            (ImSDLGPUCommandBuffer*)commandBuffer.Handle
        );
    }

    private static unsafe void RenderImGuiBuffers(
        ImDrawDataPtr drawData,
        CommandBuffer commandBuffer,
        RenderPass guiRenderPass)
    {
        ImGuiImplSDL3.SDLGPU3RenderDrawData(
            drawData, 
            (ImSDLGPUCommandBuffer*)commandBuffer.Handle, 
            (ImSDLGPURenderPass*)guiRenderPass.Handle, 
            null
        );
    }

    private unsafe void ProcessEvents(SDL.SDL_Event e)
    {
        ImGuiImplSDL3.ProcessEvent((ImSDLEvent*)&e);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (Sampler sampler in Samplers)
            {
                sampler.Dispose();
            }
        }

        ImGuiImplSDL3.Shutdown();
        ImGuiImplSDL3.SDLGPU3Shutdown();

        ImGui.SetCurrentContext(null);
        ImGui.DestroyContext();
    }

    ~ImGuiBackend()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public Sampler SamplerForType(ImGuiSamplerType samplerType)
    {
        return Samplers[(int)samplerType];
    }

    /// <summary>
    /// Temporary fix for Hexa's binding being out-of-date:
    /// https://discord.com/channels/882227476166758410/1436449375445323968/1436449375445323968
    /// </summary>
    private unsafe struct Fixed_ImGuiImplSDLGPU3InitInfo
    {
        public SDLGPUDevice* Device;
        public int ColorTargetFormat;
        public int MSAASamples;
        // Only used in multi-viewports mode.
        public SwapchainComposition SwapchainComposition;
        // Only used in multi-viewports mode.
        public PresentMode PresentMode;
    }

    private void InitImGuiRenderingDetails()
    {
        var mainWindow = Game.MainWindow!;
        var graphicsDevice = Game.GraphicsDevice!;

        var ctx = ImGui.CreateContext();
        ImGui.SetCurrentContext(ctx);
        ImGuiIOPtr io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard
                        | ImGuiConfigFlags.NavEnableGamepad
                        | ImGuiConfigFlags.DockingEnable
                        | ImGuiConfigFlags.ViewportsEnable;

        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();
        var mainScale = mainWindow.DisplayScale;
        style.ScaleAllSizes(mainScale);
        style.FontScaleDpi = mainScale;

        // Automatically overwrite style.FontScaleDpi in Begin() when Monitor DPI changes. This will scale fonts but _NOT_ scale sizes/padding for now.
        io.ConfigDpiScaleFonts = true;
        // Scale Dear ImGui and Platform Windows when Monitor DPI changes.
        io.ConfigDpiScaleViewports = true;

        // If multi-viewports are enabled, see this FAQ about coordinate system:
        // https://github.com/ocornut/imgui/wiki/Multi-Viewports#faq
        if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            var noPlatformViewports 
                = (io.BackendFlags & ImGuiBackendFlags.PlatformHasViewports) == 0;

            var noRendererViewports
                = (io.BackendFlags & ImGuiBackendFlags.RendererHasViewports) == 0;

            if (noPlatformViewports || noRendererViewports)
            {
                Logger.LogError($"Multi-viewports not supported by backend! Platform support?: {!noPlatformViewports}, Renderer support?: {!noRendererViewports}");
            }
            else
            {
                style.WindowRounding = 0.0f;
                style.Colors[(int)ImGuiCol.WindowBg].W = 1.0f;
            }
        }

        ImGuiImplSDL3.SetCurrentContext(ctx);
        unsafe
        {
            ImGuiImplSDL3.InitForSDLGPU((ImSDLWindow*)mainWindow.Handle);

            Fixed_ImGuiImplSDLGPU3InitInfo initInfo = new()
            {
                Device = (ImSDLGPUDevice*)graphicsDevice.Handle,
                ColorTargetFormat = GetSwapchainColorTargetFormat(),
                MSAASamples = (int)SDL.SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
                SwapchainComposition = mainWindow.SwapchainComposition,
                PresentMode = mainWindow.PresentMode
            };

            var initInfoPtr = &initInfo;
            ImGuiImplSDL3.SDLGPU3Init((ImGuiImplSDLGPU3InitInfo*)initInfoPtr);
        }
    }

    // Re-implements MoonWork's private 
    // `GraphicsDevice.GetSwapchainFormat` method.
    private int GetSwapchainColorTargetFormat()
    {
        if (!Game.MainWindow!.Claimed)
        {
            throw new System.ArgumentException(
                "Cannot get swapchain format of unclaimed window!"
            );
        }

        return (int)SDL.SDL_GetGPUSwapchainTextureFormat(
            Game.GraphicsDevice.Handle, 
            Game.MainWindow.Handle
        );
    }
}