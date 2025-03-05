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

		GameState CurrentState;

		public RollAndCashGame(
			AppInfo appInfo,
			WindowCreateInfo windowCreateInfo,
			FramePacingSettings framePacingSettings,
			ShaderFormat shaderFormats,
			bool debugMode
		) : base(appInfo, windowCreateInfo, framePacingSettings, shaderFormats, debugMode)
		{
			Inputs.Mouse.Hide();

			TextureAtlases.Init(GraphicsDevice);
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

			CurrentState.Update(dt);
		}

		protected override void Draw(double alpha)
		{
			CurrentState.Draw(MainWindow, alpha);
		}

		protected override void Destroy()
		{

		}

		public void SetState(GameState gameState)
		{
			if (CurrentState != null)
			{
				CurrentState.End();
			}

			gameState.Start();
			CurrentState = gameState;
		}
	}
}
