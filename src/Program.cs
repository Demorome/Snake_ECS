using System;
using System.IO;
using MoonWorks;

namespace Snake
{
	class Program
	{
		public static string UserDataDirectory = $"{Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "SNAKE")}";

		static void Main(string[] args)
		{
			if (!System.IO.Directory.Exists(UserDataDirectory))
			{
				System.IO.Directory.CreateDirectory(UserDataDirectory);
			}

			AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;

#if DEBUG
			WindowCreateInfo windowCreateInfo = new WindowCreateInfo
			{
				WindowWidth = 1280,
				WindowHeight = 720,
				WindowTitle = "SNAKE",
				ScreenMode = ScreenMode.Windowed
			};
#else
			WindowCreateInfo windowCreateInfo = new WindowCreateInfo
			{
				WindowWidth = 1280,
				WindowHeight = 720,
				WindowTitle = "SNAKE",
				ScreenMode = ScreenMode.Fullscreen
			};
#endif

			FramePacingSettings framePacingSettings = FramePacingSettings.CreateLatencyOptimized(60);

			var debugMode = false;

#if DEBUG
			debugMode = true;
#endif

			var appInfo = new AppInfo("Quebecwhats", "Snake");
			SnakeGame game = new SnakeGame(
				appInfo,
				windowCreateInfo,
				framePacingSettings,
				MoonWorks.Graphics.ShaderFormat.SPIRV | MoonWorks.Graphics.ShaderFormat.DXBC,
				debugMode
			);

			game.Run();
		}

		static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args)
		{
			Exception e = (Exception)args.ExceptionObject;
			Logger.LogError("Unhandled exception caught!");
			Logger.LogError(e.ToString());

			Game.ShowRuntimeError("FLAGRANT SYSTEM ERROR", e.ToString());

			StreamWriter streamWriter = new StreamWriter(Path.Combine(UserDataDirectory, "log.txt"));

			streamWriter.WriteLine(e.ToString());
			streamWriter.Flush();
			streamWriter.Close();
		}
	}
}
