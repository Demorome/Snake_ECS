using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.AsyncIO;
using MoonWorks.Graphics;
using MoonWorks.Input;
using RollAndCash.Components;
using SDL3;
using Buffer = MoonWorks.Graphics.Buffer;

namespace RollAndCash;

// Credits to @darkerbit: https://gist.github.com/darkerbit/6bfb661d7ce9263ddd7dcc7b475460e0
public class ImGuiBackend : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Vertex : IVertexType
    {
        public Vector2 Position;
        public Vector2 TexCoord;
        public Color Color;

        public static VertexElementFormat[] Formats { get; } =
        [
            VertexElementFormat.Float2,
            VertexElementFormat.Float2,
            VertexElementFormat.Ubyte4Norm,
        ];

        public static uint[] Offsets { get; } =
        [
            0,
            8,
            16,
        ];
    }

    public enum SamplerType
    {
        LinearClamp = 0,
        LinearWrap = 1,
        PointClamp = 2,
        PointWrap = 3,
    }

    public static ImGuiBackend Instance { get; private set; }

    public bool WantCaptureMouse { get; private set; }
    public bool WantCaptureKeyboard { get; private set; }
    public bool WantTextInput { get; private set; }

    private Game Game { get; }

    private Shader VertexShader { get; }
    private Shader FragmentShader { get; }

    private Texture fontAtlasTex;

    private Sampler[] samplers;

    private Dictionary<IntPtr, TextureSamplerBinding> boundTextures;

    private GraphicsPipeline pipeline;

    private uint vertexCount, indexCount;
    private Buffer vertexBuf, indexBuf;
    private TransferBuffer vertexTransBuf, indexTransBuf;

    // stops clipboard delegates from being gc'd
    // ReSharper disable once CollectionNeverQueried.Local
    private Dictionary<Delegate, IntPtr> pinnedDelegates = new Dictionary<Delegate, IntPtr>();

    private IntPtr PinDelegate<T>(T func) where T : Delegate
    {
        IntPtr ptr = Marshal.GetFunctionPointerForDelegate(func);
        pinnedDelegates.Add(func, ptr);
        return ptr;
    }

    private static unsafe string GetClipboard(void* userdata)
    {
        return SDL.SDL_GetClipboardText();
    }

    private static unsafe void SetClipboard(void* userdata, string text)
    {
        SDL.SDL_SetClipboardText(text);
    }

    public unsafe ImGuiBackend(Game game)
    {
        Instance = this;

        Game = game;

        var shaderContentPath = "Content/Shaders";

		var vertShader = ShaderCross.Create(Game.GraphicsDevice, Game.RootTitleStorage,
            $"{shaderContentPath}/ImGui.vert.hlsl.spv", "main", ShaderCross.ShaderFormat.SPIRV, ShaderStage.Vertex);

		var fragShader = ShaderCross.Create(Game.GraphicsDevice, Game.RootTitleStorage,
            $"{shaderContentPath}/ImGui.frag.hlsl.spv", "main", ShaderCross.ShaderFormat.SPIRV, ShaderStage.Fragment);

        RebuildPipeline(vertShader, fragShader);

        vertShader.Dispose();
        fragShader.Dispose();

        samplers =
        [
            Sampler.Create(Game.GraphicsDevice, "Dear ImGui Linear Clamp Sampler", SamplerCreateInfo.LinearClamp),
            Sampler.Create(Game.GraphicsDevice, "Dear ImGui Linear Wrap Sampler", SamplerCreateInfo.LinearWrap),
            Sampler.Create(Game.GraphicsDevice, "Dear ImGui Point Clamp Sampler", SamplerCreateInfo.PointClamp),
            Sampler.Create(Game.GraphicsDevice, "Dear ImGui Point Wrap Sampler", SamplerCreateInfo.PointWrap),
        ];

        boundTextures = new Dictionary<IntPtr, TextureSamplerBinding>();

        ImGui.CreateContext();
        ReuploadFontAtlas();

        Inputs.TextInput += OnTextInput;

        ImGuiPlatformIOPtr pio = ImGui.GetPlatformIO();

        /* FIXME: Use latest Imgui.NET

        pio.Platform_GetClipboardTextFn = PinDelegate(GetClipboard);
        pio.Platform_SetClipboardTextFn = PinDelegate(SetClipboard);
        */
    }

    public void RebuildPipeline(Shader vertexShader, Shader fragmentShader)
    {
        pipeline?.Dispose();

        pipeline = GraphicsPipeline.Create(
            Game.GraphicsDevice,
            new GraphicsPipelineCreateInfo
            {
                Name = "Dear ImGui Graphics Pipeline",
                PrimitiveType = PrimitiveType.TriangleList,
                RasterizerState = RasterizerState.CCW_CullNone,
                DepthStencilState = DepthStencilState.Disable,
                MultisampleState = MultisampleState.None,
                VertexInputState = VertexInputState.CreateSingleBinding<Vertex>(),
                VertexShader = vertexShader,
                FragmentShader = fragmentShader,
                TargetInfo = new GraphicsPipelineTargetInfo
                {
                    ColorTargetDescriptions =
                    [
                        new ColorTargetDescription
                        {
                            Format = Game.MainWindow.SwapchainFormat,
                            BlendState = ColorTargetBlendState.NonPremultipliedAlphaBlend,
                        },
                    ],
                    HasDepthStencilTarget = false,
                },
            }
        );
    }

    public unsafe void ReuploadFontAtlas()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* data, out int width, out int height, out int bytesPerPixel);

        fontAtlasTex?.Dispose();

        using (ResourceUploader uploader = new ResourceUploader(Game.GraphicsDevice))
        {
            fontAtlasTex = uploader.CreateTexture2D(
                "Dear ImGui Font Atlas Texture",
                new ReadOnlySpan<byte>(data, width * height * bytesPerPixel),
                TextureFormat.R8G8B8A8Unorm,
                TextureUsageFlags.Sampler,
                (uint)width,
                (uint)height
            );
            uploader.Upload();
        }

        io.Fonts.SetTexID(IntPtr.Zero);
        io.Fonts.ClearTexData();
    }

    public void NewFrame(TimeSpan delta)
    {
        ImGuiIOPtr io = ImGui.GetIO();

        io.DeltaTime = (float)delta.TotalSeconds;
        io.DisplaySize = new Vector2(Game.MainWindow.Width, Game.MainWindow.Height);

        WantCaptureMouse = io.WantCaptureMouse;
        WantCaptureKeyboard = io.WantCaptureKeyboard;

        if (io.WantTextInput && !WantTextInput)
        {
            Game.MainWindow.StartTextInput();
            WantTextInput = true;
        }
        else if (!io.WantTextInput && WantTextInput)
        {
            Game.MainWindow.StopTextInput();
            WantTextInput = false;
        }

        UpdateMouse(io);
        UpdateKeyboard(io);

        ImGui.NewFrame();

        boundTextures.Clear();
    }

    private void UpdateMouse(ImGuiIOPtr io)
    {
        io.AddMousePosEvent(Game.Inputs.Mouse.X, Game.Inputs.Mouse.Y);
        io.AddMouseWheelEvent(0.0f, Game.Inputs.Mouse.Wheel);

        if (Game.Inputs.Mouse.LeftButton.IsPressed || Game.Inputs.Mouse.LeftButton.IsReleased)
        {
            io.AddMouseButtonEvent(0, Game.Inputs.Mouse.LeftButton.IsDown);
        }

        if (Game.Inputs.Mouse.RightButton.IsPressed || Game.Inputs.Mouse.RightButton.IsReleased)
        {
            io.AddMouseButtonEvent(1, Game.Inputs.Mouse.RightButton.IsDown);
        }

        if (Game.Inputs.Mouse.MiddleButton.IsPressed || Game.Inputs.Mouse.MiddleButton.IsReleased)
        {
            io.AddMouseButtonEvent(2, Game.Inputs.Mouse.MiddleButton.IsDown);
        }

        if (Game.Inputs.Mouse.X1Button.IsPressed || Game.Inputs.Mouse.X1Button.IsReleased)
        {
            io.AddMouseButtonEvent(3, Game.Inputs.Mouse.X1Button.IsDown);
        }

        if (Game.Inputs.Mouse.X2Button.IsPressed || Game.Inputs.Mouse.X2Button.IsReleased)
        {
            io.AddMouseButtonEvent(4, Game.Inputs.Mouse.X2Button.IsDown);
        }
    }

    private void OnTextInput(char c)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.AddInputCharacter(c);
    }

    private void UpdateKeyboard(ImGuiIOPtr io)
    {
        io.AddKeyEvent(
            ImGuiKey.ModCtrl,
            Game.Inputs.Keyboard.IsDown(KeyCode.LeftControl) || Game.Inputs.Keyboard.IsDown(KeyCode.RightControl)
        );

        io.AddKeyEvent(
            ImGuiKey.ModAlt,
            Game.Inputs.Keyboard.IsDown(KeyCode.LeftAlt) || Game.Inputs.Keyboard.IsDown(KeyCode.RightAlt)
        );

        io.AddKeyEvent(
            ImGuiKey.ModShift,
            Game.Inputs.Keyboard.IsDown(KeyCode.LeftShift) || Game.Inputs.Keyboard.IsDown(KeyCode.RightShift)
        );

        io.AddKeyEvent(
            ImGuiKey.ModSuper,
            Game.Inputs.Keyboard.IsDown(KeyCode.LeftMeta) || Game.Inputs.Keyboard.IsDown(KeyCode.RightMeta)
        );

        foreach (KeyCode key in keyCodes)
        {
            if (Game.Inputs.Keyboard.IsPressed(key) || Game.Inputs.Keyboard.IsReleased(key))
            {
                io.AddKeyEvent(KeyCodeToImGui(key), Game.Inputs.Keyboard.IsDown(key));
            }
        }
    }

    public void EndFrame()
    {
        ImGui.EndFrame();
    }

    public unsafe void UploadBuffers(CommandBuffer cb)
    {
        ImGui.Render();

        ImDrawDataPtr drawData = ImGui.GetDrawData();

        if (drawData.TotalVtxCount == 0)
        {
            return;
        }

        if (drawData.TotalVtxCount > vertexCount)
        {
            vertexBuf?.Dispose();
            vertexTransBuf?.Dispose();

            vertexCount = (uint)drawData.TotalVtxCount * 2;

            vertexBuf = Buffer.Create<Vertex>(
                Game.GraphicsDevice,
                "Dear ImGui Vertex Buffer",
                BufferUsageFlags.Vertex,
                vertexCount
            );

            vertexTransBuf = TransferBuffer.Create<Vertex>(
                Game.GraphicsDevice,
                "Dear ImGui Vertex Transfer Buffer",
                TransferBufferUsage.Upload,
                vertexCount
            );
        }

        if (drawData.TotalIdxCount > indexCount)
        {
            indexBuf?.Dispose();
            indexTransBuf?.Dispose();

            indexCount = (uint)drawData.TotalIdxCount * 2;

            indexBuf = Buffer.Create<ushort>(
                Game.GraphicsDevice,
                "Dear ImGui Index Buffer",
                BufferUsageFlags.Index,
                indexCount
            );

            indexTransBuf = TransferBuffer.Create<ushort>(
                Game.GraphicsDevice,
                "Dear ImGui Index Transfer Buffer",
                TransferBufferUsage.Upload,
                indexCount
            );
        }

        vertexTransBuf.Map(true);
        indexTransBuf.Map(true);

        uint vertexOffset = 0;
        uint indexOffset = 0;

        for (int i = 0; i < drawData.CmdListsCount; i++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[i];

            Span<Vertex> vertexSpan = new Span<Vertex>(cmdList.VtxBuffer.Data.ToPointer(), cmdList.VtxBuffer.Size);
            Span<ushort> indexSpan = new Span<ushort>(cmdList.IdxBuffer.Data.ToPointer(), cmdList.IdxBuffer.Size);

            vertexSpan.CopyTo(vertexTransBuf.MappedSpan<Vertex>((uint)(vertexOffset * sizeof(Vertex))));
            indexSpan.CopyTo(indexTransBuf.MappedSpan<ushort>((uint)(indexOffset * sizeof(ushort))));

            vertexOffset += (uint)cmdList.VtxBuffer.Size;
            indexOffset += (uint)cmdList.IdxBuffer.Size;
        }

        vertexTransBuf.Unmap();
        indexTransBuf.Unmap();

        cb.PushDebugGroup("Dear ImGui Buffer Uploads");

        CopyPass copyPass = cb.BeginCopyPass();
        copyPass.UploadToBuffer(vertexTransBuf, vertexBuf, true);
        copyPass.UploadToBuffer(indexTransBuf, indexBuf, true);
        cb.EndCopyPass(copyPass);

        cb.PopDebugGroup();
    }

    public void Render(RenderPass pass)
    {
        ImDrawDataPtr drawData = ImGui.GetDrawData();

        if (drawData.TotalVtxCount == 0)
        {
            return;
        }

        pass.CommandBuffer.PushDebugGroup("Dear ImGui Render");

        pass.CommandBuffer.PushVertexUniformData(
            Matrix4x4.CreateOrthographicOffCenter(
                0.0f, Game.MainWindow.Width, Game.MainWindow.Height, 0.0f, -1.0f, 1.0f
            )
        );

        pass.BindGraphicsPipeline(pipeline);
        pass.BindVertexBuffers(vertexBuf);
        pass.BindIndexBuffer(indexBuf, IndexElementSize.Sixteen);

        uint vertexOffset = 0;
        uint indexOffset = 0;

        for (int j = 0; j < drawData.CmdListsCount; j++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[j];

            for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
            {
                ImDrawCmdPtr drawCmd = cmdList.CmdBuffer[i];

                Vector2 clipMin = new Vector2(
                    Math.Max(0.0f, drawCmd.ClipRect.X),
                    Math.Max(0.0f, drawCmd.ClipRect.Y)
                );

                Vector2 clipMax = new Vector2(
                    Math.Min(drawCmd.ClipRect.Z, Game.MainWindow.Width),
                    Math.Min(drawCmd.ClipRect.W, Game.MainWindow.Height)
                );

                if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y)
                {
                    continue;
                }

                pass.SetScissor(new Rect(
                    (int)clipMin.X,
                    (int)clipMin.Y,
                    (int)(clipMax.X - clipMin.X),
                    (int)(clipMax.Y - clipMin.Y)
                ));

                pass.BindFragmentSamplers(GetTextureBinding(drawCmd.TextureId));

                pass.DrawIndexedPrimitives(
                    drawCmd.ElemCount,
                    1,
                    indexOffset + drawCmd.IdxOffset,
                    (int)vertexOffset + (int)drawCmd.VtxOffset,
                    0
                );
            }

            vertexOffset += (uint)cmdList.VtxBuffer.Size;
            indexOffset += (uint)cmdList.IdxBuffer.Size;
        }

        pass.CommandBuffer.PopDebugGroup();
    }

    public IntPtr BindTexture(Texture texture, SamplerType samplerType = SamplerType.LinearClamp)
    {
        Sampler sampler = samplers[(int)samplerType];

        if (boundTextures.TryGetValue(texture.Handle, out TextureSamplerBinding binding))
        {
            if (binding.Sampler != sampler.Handle)
            {
                throw new Exception("BAD WRONG tried to bind same texture multiple times with different samplers!");
            }
        }
        else
        {
            boundTextures.Add(texture.Handle, new TextureSamplerBinding(texture, sampler));
        }

        return texture.Handle;
    }

    private TextureSamplerBinding GetTextureBinding(IntPtr id)
    {
        return id == IntPtr.Zero
            ? new TextureSamplerBinding(fontAtlasTex, samplers[(int)SamplerType.LinearClamp])
            : boundTextures[id];
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            fontAtlasTex.Dispose();

            foreach (Sampler sampler in samplers)
            {
                sampler.Dispose();
            }

            pipeline.Dispose();

            vertexBuf?.Dispose();
            vertexTransBuf?.Dispose();

            indexBuf?.Dispose();
            indexTransBuf?.Dispose();

            Instance = null;

            Inputs.TextInput -= OnTextInput;
        }

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

    private static KeyCode[] keyCodes = Enum.GetValues<KeyCode>();

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
            KeyCode.D1 => ImGuiKey._1,
            KeyCode.D2 => ImGuiKey._2,
            KeyCode.D3 => ImGuiKey._3,
            KeyCode.D4 => ImGuiKey._4,
            KeyCode.D5 => ImGuiKey._5,
            KeyCode.D6 => ImGuiKey._6,
            KeyCode.D7 => ImGuiKey._7,
            KeyCode.D8 => ImGuiKey._8,
            KeyCode.D9 => ImGuiKey._9,
            KeyCode.D0 => ImGuiKey._0,
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
}

public static class ImGuiExtensions
{
	public static void Image(
		Texture texture,
		Vector2 imageSize,
		ImGuiBackend.SamplerType samplerType = ImGuiBackend.SamplerType.LinearClamp
	)
	{
		ImGui.Image(
			ImGuiBackend.Instance.BindTexture(texture, samplerType),
			imageSize
		);
	}

	public static void Image(
		Texture texture,
		Vector2 imageSize,
		Vector2 uv0,
		ImGuiBackend.SamplerType samplerType = ImGuiBackend.SamplerType.LinearClamp
	)
	{
		ImGui.Image(
			ImGuiBackend.Instance.BindTexture(texture, samplerType),
			imageSize,
			uv0
		);
	}

	public static void Image(
		Texture texture,
		Vector2 imageSize,
		Vector2 uv0,
		Vector2 uv1,
		ImGuiBackend.SamplerType samplerType = ImGuiBackend.SamplerType.LinearClamp
	)
	{
		ImGui.Image(
			ImGuiBackend.Instance.BindTexture(texture, samplerType),
			imageSize,
			uv0,
			uv1
		);
	}

	public static void Image(
		Texture texture,
		Vector2 imageSize,
		Vector2 uv0,
		Vector2 uv1,
		Vector4 tintColor,
		ImGuiBackend.SamplerType samplerType = ImGuiBackend.SamplerType.LinearClamp
	)
	{
		ImGui.Image(
			ImGuiBackend.Instance.BindTexture(texture, samplerType),
			imageSize,
			uv0,
			uv1,
			tintColor
		);
	}

	public static void Image(
		Texture texture,
		Vector2 imageSize,
		Vector2 uv0,
		Vector2 uv1,
		Vector4 tintColor,
		Vector4 borderColor,
		ImGuiBackend.SamplerType samplerType = ImGuiBackend.SamplerType.LinearClamp
	)
	{
		ImGui.Image(
			ImGuiBackend.Instance.BindTexture(texture, samplerType),
			imageSize,
			uv0,
			uv1,
			tintColor,
			borderColor
		);
	}

	public static bool ImageButton(
		string id,
		Texture texture,
		Vector2 imageSize,
		ImGuiBackend.SamplerType samplerType = ImGuiBackend.SamplerType.LinearClamp
	)
	{
		return ImGui.ImageButton(
			id,
			ImGuiBackend.Instance.BindTexture(texture, samplerType),
			imageSize
		);
	}

	public static bool ImageButton(
		string id,
		Texture texture,
		Vector2 imageSize,
		Vector2 uv0,
		ImGuiBackend.SamplerType samplerType = ImGuiBackend.SamplerType.LinearClamp
	)
	{
		return ImGui.ImageButton(
			id,
			ImGuiBackend.Instance.BindTexture(texture, samplerType),
			imageSize,
			uv0
		);
	}

	public static bool ImageButton(
		string id,
		Texture texture,
		Vector2 imageSize,
		Vector2 uv0,
		Vector2 uv1,
		ImGuiBackend.SamplerType samplerType = ImGuiBackend.SamplerType.LinearClamp
	)
	{
		return ImGui.ImageButton(
			id,
			ImGuiBackend.Instance.BindTexture(texture, samplerType),
			imageSize,
			uv0,
			uv1
		);
	}

	public static bool ImageButton(
		string id,
		Texture texture,
		Vector2 imageSize,
		Vector2 uv0,
		Vector2 uv1,
		Vector4 bgCol,
		ImGuiBackend.SamplerType samplerType = ImGuiBackend.SamplerType.LinearClamp
	)
	{
		return ImGui.ImageButton(
			id,
			ImGuiBackend.Instance.BindTexture(texture, samplerType),
			imageSize,
			uv0,
			uv1,
			bgCol
		);
	}

	public static bool ImageButton(
		string id,
		Texture texture,
		Vector2 imageSize,
		Vector2 uv0,
		Vector2 uv1,
		Vector4 bgCol,
		Vector4 tintCol,
		ImGuiBackend.SamplerType samplerType = ImGuiBackend.SamplerType.LinearClamp
	)
	{
		return ImGui.ImageButton(
			id,
			ImGuiBackend.Instance.BindTexture(texture, samplerType),
			imageSize,
			uv0,
			uv1,
			bgCol,
			tintCol
		);
	}
}