#if DEBUG
#define UseDebugGUI
#endif

using MoonWorks.Graphics;
using MoonWorks;
using RollAndCash.Content;
using RollAndCash.GameStates;

namespace RollAndCash
{
	public class RollAndCashGame : Game
	{
		LoadState LoadState;
		LogoState LogoState;
		GameplayState GameplayState;
		TitleState TitleState;

		GameState? CurrentState;

#if UseDebugGUI
		ImGuiBackend ImGuiBackend;
#endif

		public RollAndCashGame(
			AppInfo appInfo,
			WindowCreateInfo windowCreateInfo,
			FramePacingSettings framePacingSettings,
			ShaderFormat shaderFormats,
			bool debugMode
		) : base(appInfo, 
				windowCreateInfo, 
				framePacingSettings, 
				shaderFormats, 
				debugMode
			)
		{
#if UseDebugGUI
			ImGuiBackend = new(this);
			Editor.DrawComponents.ReInitComponentTypesList();
#else
			Inputs.Mouse.Hide();
#endif

			TextureAtlases.Init(GraphicsDevice);
			TileSetAtlases.Init(GraphicsDevice);
			StaticAudioPacks.Init(AudioDevice);
			StreamingAudio.Init(AudioDevice);
			Fonts.LoadAll(GraphicsDevice, RootTitleStorage);

			LogoState = new LogoState(this, null);
			TitleState = new TitleState(this, LogoState, null);
			LoadState = new LoadState(this, LogoState);

			GameplayState = new GameplayState(this, TitleState);
			TitleState.SetTransitionStateB(GameplayState);
			LogoState.SetTransitionState(TitleState);

			SetState(LoadState);
		}

		protected override void Update(System.TimeSpan dt)
		{
			if (Inputs.Keyboard.IsPressed(MoonWorks.Input.KeyCode.F11))
			{
				if (MainWindow.ScreenMode == ScreenMode.Fullscreen)
					MainWindow.SetScreenMode(ScreenMode.Windowed);
				else
					MainWindow.SetScreenMode(ScreenMode.Fullscreen);

			}

#if UseDebugGUI
			ImGuiBackend.NewFrame();
#endif

			CurrentState?.Update(dt);

#if UseDebugGUI
			ImGuiBackend.EndFrame();
#endif
		}

		protected override void Step()
        {
            
        }

		protected override void Draw(double alpha)
		{
			var commandBuffer = GraphicsDevice.AcquireCommandBuffer();
			var swapchainTexture = commandBuffer.AcquireSwapchainTexture(MainWindow);
			if (swapchainTexture != null)
			{
				CurrentState?.Draw(
					commandBuffer, 
					swapchainTexture, 
					MainWindow, 
					alpha
				);

#if UseDebugGUI
				ImGuiBackend.UploadAndRenderBuffers(
					commandBuffer,
					new ColorTargetInfo(
						swapchainTexture, 
						LoadOp.Load, 
						false
					)
				);
#endif
			}

#if UseDebugGUI
			ImGuiBackend.RenderOtherPlatformWindows();
#endif

			// You must always submit the command buffer.
			GraphicsDevice.Submit(commandBuffer);
		}

		protected override void Destroy()
		{
#if UseDebugGUI
			ImGuiBackend.Dispose();
#endif
		}

		public void SetState(GameState? gameState)
		{
			if (CurrentState != null)
			{
				CurrentState.End();
			}

			if (gameState != null)
			{
				gameState.Start();
			}
			CurrentState = gameState;
		}
    }
}
