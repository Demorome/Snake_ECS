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

    public void NewFrame(TimeSpan delta)
    {
        ImGuiIOPtr io = ImGui.GetIO();

        // FIXME: Might not need these anymore!
        io.DeltaTime = (float)delta.TotalSeconds;
        io.DisplaySize = new Vector2(Game.MainWindow.Width, Game.MainWindow.Height);

        ImGuiImplSDL3.SDLGPU3NewFrame();
        ImGuiImplSDL3.NewFrame();
        ImGui.NewFrame();
    }

    public void EndFrame()
    {
        ImGui.EndFrame();
    }

    /// <summary>
    /// Will perform the render pass using ColorTargetInfo.
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
        // Update and Render additional Platform Windows.
        // https://github.com/ocornut/imgui/wiki/Multi-Viewports#how-can-i-enable-multi-viewports-
        if ((ImGui.GetIO().ConfigFlags 
            & ImGuiConfigFlags.ViewportsEnable) != 0)
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

    private static KeyCode[] _KeyCodes = Enum.GetValues<KeyCode>();

    private static ImGuiKey KeyCodeToImGui(KeyCode key)
    {
        return key switch
        {
            KeyCode.Unknown => ImGuiKey.None,
            KeyCode.A => ImGuiKey.A,
            KeyCode.B => ImGuiKey.B,
            KeyCode.C => ImGuiKey.C,
            KeyCode.D => ImGuiKey.D,
            KeyCode.E => ImGuiKey.E,
            KeyCode.F => ImGuiKey.F,
            KeyCode.G => ImGuiKey.G,
            KeyCode.H => ImGuiKey.H,
            KeyCode.I => ImGuiKey.I,
            KeyCode.J => ImGuiKey.J,
            KeyCode.K => ImGuiKey.K,
            KeyCode.L => ImGuiKey.L,
            KeyCode.M => ImGuiKey.M,
            KeyCode.N => ImGuiKey.N,
            KeyCode.O => ImGuiKey.O,
            KeyCode.P => ImGuiKey.P,
            KeyCode.Q => ImGuiKey.Q,
            KeyCode.R => ImGuiKey.R,
            KeyCode.S => ImGuiKey.S,
            KeyCode.T => ImGuiKey.T,
            KeyCode.U => ImGuiKey.U,
            KeyCode.V => ImGuiKey.V,
            KeyCode.W => ImGuiKey.W,
            KeyCode.X => ImGuiKey.X,
            KeyCode.Y => ImGuiKey.Y,
            KeyCode.Z => ImGuiKey.Z,
            KeyCode.D1 => ImGuiKey.Key1,
            KeyCode.D2 => ImGuiKey.Key2,
            KeyCode.D3 => ImGuiKey.Key3,
            KeyCode.D4 => ImGuiKey.Key4,
            KeyCode.D5 => ImGuiKey.Key5,
            KeyCode.D6 => ImGuiKey.Key6,
            KeyCode.D7 => ImGuiKey.Key7,
            KeyCode.D8 => ImGuiKey.Key8,
            KeyCode.D9 => ImGuiKey.Key9,
            KeyCode.D0 => ImGuiKey.Key0,
            KeyCode.Return => ImGuiKey.Enter,
            KeyCode.Escape => ImGuiKey.Escape,
            KeyCode.Backspace => ImGuiKey.Backspace,
            KeyCode.Tab => ImGuiKey.Tab,
            KeyCode.Space => ImGuiKey.Space,
            KeyCode.Minus => ImGuiKey.Minus,
            KeyCode.Equals => ImGuiKey.Equal,
            KeyCode.LeftBracket => ImGuiKey.LeftBracket,
            KeyCode.RightBracket => ImGuiKey.RightBracket,
            KeyCode.Backslash => ImGuiKey.Backslash,
            KeyCode.Semicolon => ImGuiKey.Semicolon,
            KeyCode.Apostrophe => ImGuiKey.Apostrophe,
            KeyCode.Grave => ImGuiKey.GraveAccent,
            KeyCode.Comma => ImGuiKey.Comma,
            KeyCode.Period => ImGuiKey.Period,
            KeyCode.Slash => ImGuiKey.Slash,
            KeyCode.CapsLock => ImGuiKey.CapsLock,
            KeyCode.F1 => ImGuiKey.F1,
            KeyCode.F2 => ImGuiKey.F2,
            KeyCode.F3 => ImGuiKey.F3,
            KeyCode.F4 => ImGuiKey.F4,
            KeyCode.F5 => ImGuiKey.F5,
            KeyCode.F6 => ImGuiKey.F6,
            KeyCode.F7 => ImGuiKey.F7,
            KeyCode.F8 => ImGuiKey.F8,
            KeyCode.F9 => ImGuiKey.F9,
            KeyCode.F10 => ImGuiKey.F10,
            KeyCode.F11 => ImGuiKey.F11,
            KeyCode.F12 => ImGuiKey.F12,
            KeyCode.PrintScreen => ImGuiKey.PrintScreen,
            KeyCode.ScrollLock => ImGuiKey.ScrollLock,
            KeyCode.Pause => ImGuiKey.Pause,
            KeyCode.Insert => ImGuiKey.Insert,
            KeyCode.Home => ImGuiKey.Home,
            KeyCode.PageUp => ImGuiKey.PageUp,
            KeyCode.Delete => ImGuiKey.Delete,
            KeyCode.End => ImGuiKey.End,
            KeyCode.PageDown => ImGuiKey.PageDown,
            KeyCode.Right => ImGuiKey.RightArrow,
            KeyCode.Left => ImGuiKey.LeftArrow,
            KeyCode.Down => ImGuiKey.DownArrow,
            KeyCode.Up => ImGuiKey.UpArrow,
            KeyCode.NumLockClear => ImGuiKey.NumLock,
            KeyCode.KeypadDivide => ImGuiKey.KeypadDivide,
            KeyCode.KeypadMultiply => ImGuiKey.KeypadMultiply,
            KeyCode.KeypadMinus => ImGuiKey.KeypadSubtract,
            KeyCode.KeypadPlus => ImGuiKey.KeypadAdd,
            KeyCode.KeypadEnter => ImGuiKey.KeypadEnter,
            KeyCode.Keypad1 => ImGuiKey.Keypad1,
            KeyCode.Keypad2 => ImGuiKey.Keypad2,
            KeyCode.Keypad3 => ImGuiKey.Keypad3,
            KeyCode.Keypad4 => ImGuiKey.Keypad4,
            KeyCode.Keypad5 => ImGuiKey.Keypad5,
            KeyCode.Keypad6 => ImGuiKey.Keypad6,
            KeyCode.Keypad7 => ImGuiKey.Keypad7,
            KeyCode.Keypad8 => ImGuiKey.Keypad8,
            KeyCode.Keypad9 => ImGuiKey.Keypad9,
            KeyCode.Keypad0 => ImGuiKey.Keypad0,
            KeyCode.KeypadPeriod => ImGuiKey.KeypadDecimal,
            KeyCode.LeftControl => ImGuiKey.LeftCtrl,
            KeyCode.LeftShift => ImGuiKey.LeftShift,
            KeyCode.LeftAlt => ImGuiKey.LeftAlt,
            KeyCode.LeftMeta => ImGuiKey.LeftSuper,
            KeyCode.RightControl => ImGuiKey.RightCtrl,
            KeyCode.RightShift => ImGuiKey.RightShift,
            KeyCode.RightAlt => ImGuiKey.RightAlt,
            KeyCode.RightMeta => ImGuiKey.RightSuper,
            _ => ImGuiKey.None,
        };
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
        var graphicsDevice = Game.GraphicsDevice;

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
        io.ConfigDpiScaleFonts = true;
        io.ConfigDpiScaleViewports = true;

        if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            style.WindowRounding = 0.0f;
            style.Colors[(int)ImGuiCol.WindowBg].W = 1.0f;
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