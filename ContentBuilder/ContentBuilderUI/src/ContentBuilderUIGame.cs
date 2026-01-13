using Hexa.NET.ImGui;
using MoonWorks.Graphics;
using MoonWorks.Input;
using MoonWorks;
using System.Numerics;
using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ContentBuilderUI
{
	class ContentBuilderUIGame : Game
	{
		private string FontContentPath = Path.Combine("Content", "Fonts");

		private string unprocessedContentPath = "";
		private string projectPath = "";

		private bool ContentPathValid = false;
		private bool ProjectPathValid = false;

		private ImGuiBackend ImGuiBackend;

		public unsafe ContentBuilderUIGame(
			AppInfo appInfo,
			WindowCreateInfo windowCreateInfo,
			FramePacingSettings frameLimiterSettings,
			bool debugMode
		) : base(appInfo, windowCreateInfo, frameLimiterSettings, ShaderFormat.SPIRV, debugMode)
		{
			Operations.Initialize();

			if (Operations.Preferences != null)
			{
				if (Operations.Preferences.SourceContentDirectoryPath != null)
				{
					unprocessedContentPath = Operations.Preferences.SourceContentDirectoryPath;
				}
				if (Operations.Preferences.GameDirectoryPath != null)
				{
					projectPath = Operations.Preferences.GameDirectoryPath;
				}

				ContentPathValid = Operations.ValidateSourceContentDirectory(unprocessedContentPath);
				ProjectPathValid = Operations.ValidateGameProjectDirectory(projectPath);
			}

			ImGuiBackend = new ImGuiBackend(this);

			/* ImGui 1.92: https://github.com/ocornut/imgui/blob/master/docs/FONTS.md#new-dynamic-fonts-system-in-192-june-2025
			* Users of icons, Asian and non-English languages do not need to pre-build all glyphs ahead of time. 
			* Saving on loading time, memory, and also reducing issues with missing glyphs. 
			* Specifying glyph ranges is not needed anymore.
			*/
			var io = ImGui.GetIO();

			// Load a first font ("Combine multiple fonts into one" example from https://github.com/ocornut/imgui/blob/master/docs/FONTS.md)
			//io.Fonts.AddFontDefault();
			var newFont = io.Fonts.AddFontFromFileTTF(
				Path.Combine(FontContentPath, "FiraCode-Regular.ttf"),
				16
			);
			Debug.Assert(newFont != null);

			var fontConfig = ImGui.ImFontConfig();
			fontConfig.MergeMode = true;

			newFont = io.Fonts.AddFontFromFileTTF(
				Path.Combine(FontContentPath, "fontello.ttf"),
				16,
				fontConfig
			);
			Debug.Assert(newFont != null);

			fontConfig.Destroy(); // FIXME: Not 100% sure it's safe to destroy.
		}

		protected override void Update(System.TimeSpan dt)
		{
			ImGuiBackend.NewFrame(/*dt*/);

			// Style
			var hover = UIColors.RGB255(73, 46, 46);
			ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 1);
			ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3);
			ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
			ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2(15, 15));
			ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(5, 1));
			ImGui.PushStyleColor(ImGuiCol.FrameBg, UIColors.Transparent);
			ImGui.PushStyleColor(ImGuiCol.FrameBgActive, hover);
			ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, hover);
			ImGui.PushStyleColor(ImGuiCol.Border, UIColors.RedText);
			ImGui.PushStyleColor(ImGuiCol.WindowBg, UIColors.Background);
			ImGui.PushStyleColor(ImGuiCol.TextDisabled, UIColors.Disabled);
			ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, hover);
			ImGui.PushStyleColor(ImGuiCol.Text, UIColors.Text);
			ImGui.PushStyleColor(ImGuiCol.Button, UIColors.Transparent);
			ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
			ImGui.PushStyleColor(ImGuiCol.ButtonActive, UIColors.SamuraiGunn2Red);
			ImGui.PushStyleColor(ImGuiCol.Separator, hover);
			ImGui.PushStyleColor(ImGuiCol.SeparatorHovered, hover);
			ImGui.PushStyleColor(ImGuiCol.PopupBg, UIColors.InkBlack);

			ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
			ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 0));
			ImGui.Begin("Main", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

			#region Content Path
			ImGui.PushStyleColor(ImGuiCol.Text, BoolToColor(ContentPathValid));
			ImGui.PushStyleColor(ImGuiCol.Border, BoolToColor(ContentPathValid));
			ImGui.Text(BoolToEmoji(ContentPathValid));
			ImGui.SameLine();
			if (ImGui.InputText("Unprocessed Content Path", ref unprocessedContentPath, 255))
			{
				ContentPathValid = Operations.ValidateSourceContentDirectory(unprocessedContentPath);
			}
			ImGui.PopStyleColor(2);
			#endregion

			#region Project Path
			ImGui.PushStyleColor(ImGuiCol.Text, BoolToColor(ProjectPathValid));
			ImGui.PushStyleColor(ImGuiCol.Border, BoolToColor(ProjectPathValid));
			ImGui.Text(BoolToEmoji(ProjectPathValid));
			ImGui.SameLine();
			if (ImGui.InputText("Project Path", ref projectPath, 255))
			{
				ProjectPathValid = Operations.ValidateGameProjectDirectory(projectPath);
			}
			ImGui.PopStyleColor(2);
			#endregion

			ImGui.Spacing();
			ImGui.Spacing();

			#region Buttons
			ImGui.Columns(1, "Buttons", true);
			if (ContentPathValid && ProjectPathValid)
			{
				if (ImGui.Button("Check Content Directories"))
				{
					foreach (var trackedDirectory in Operations.AllTrackedDirectories)
					{
						Task.Run(() =>
						{
							trackedDirectory.LoadHashFromDisk();
							trackedDirectory.CalculateContentHash();
							trackedDirectory.UpdateBuildStatus();
						});
					}
				}

				ImGui.SameLine();
				if (ImGui.Button("Build Content"))
				{
					Operations.BuildOutOfDate();
				}
			}
			else
			{
				ImGui.Text("Enter Content and Project Paths to continue");
			}

			ImGui.Spacing();
			ImGui.Spacing();
			#endregion

			ImGui.Columns(2);
			ImGui.SetColumnWidth(0, 350);
			ImGui.SetColumnWidth(1, 30);
			if (ContentPathValid && ProjectPathValid)
			{
				DrawContentGroup(Operations.Sprites);
				ImGui.Separator();
				DrawContentGroup(Operations.TileSets);
				ImGui.Separator();
				DrawContentGroup(Operations.Audio);
				ImGui.Separator();
				DrawContentGroup(Operations.Fonts);
				ImGui.Separator();

				foreach (var trackedDirectory in Operations.Other)
				{
					DrawTrackedDirectory(trackedDirectory);
				}
			}

			ImGui.PopStyleVar(5);
			ImGui.PopStyleColor(14);
			ImGui.End();


			// https://github.com/ocornut/imgui/blob/8dc457fda24806d69a1b291f6fa03b9924c41fdd/docs/FONTS.md#debug-tools
			//ImGui.ShowStyleEditor(); // has a block that allows you to inspect what your fonts are, for debugging.


			ImGuiBackend.EndFrame();
		}

		protected override void Step()
        {
            
        }

		public void DrawContentGroup(ContentGroup contentGroup)
		{
			if (ImGui.TreeNodeEx(contentGroup.Name))
			{
				ImGui.NextColumn();
				DrawBuildStatus(contentGroup.BuildStatus);
				ImGui.NextColumn();

				foreach (var trackedDirectory in contentGroup)
				{
					DrawTrackedDirectory(trackedDirectory);
				}

				ImGui.TreePop();
			}
			else
			{
				ImGui.NextColumn();
				DrawBuildStatus(contentGroup.BuildStatus);
				ImGui.NextColumn();
			}
		}

		private void DrawTrackedDirectory(TrackedDirectory trackedDirectory)
		{
			var name = Path.GetFileName(trackedDirectory.DirectoryPath);
			if (trackedDirectory.BuildStatus != BuildStatus.InProgress)
			{
				if (ImGui.Button(name))
				{
					Task.Run(() => Operations.ProcessTrackedDir(trackedDirectory));
				}
				if (ImGui.IsItemHovered())
				{
					ImGui.SetTooltip("Build " + name);
				}
			}
			else
			{
				ImGui.Text(name);
			}

			ImGui.NextColumn();
			DrawBuildStatus(trackedDirectory.BuildStatus);
			ImGui.NextColumn();
		}

		private void DrawBuildStatus(BuildStatus buildStatus)
		{
			ImGui.PushStyleColor(ImGuiCol.Text, BuildStatusToColor(buildStatus));
			ImGui.Text(BuildStatusToEmoji(buildStatus));
			ImGui.PopStyleColor();
		}

		private System.Numerics.Vector4 BoolToColor(bool b)
		{
			if (b)
				return UIColors.Positive;
			else return UIColors.Negative;
		}

		private string BoolToEmoji(bool input)
		{
			return input ? "\uE800" : "\uE801";
		}

		private string BuildStatusToEmoji(BuildStatus buildStatus)
		{
			return buildStatus switch
			{
				BuildStatus.OutOfDate => "\uE801",
				BuildStatus.InProgress => "\uE832",
				BuildStatus.Comparing => "\uF0EC",
				_ => "\uE800"
			};
		}

		private System.Numerics.Vector4 BuildStatusToColor(BuildStatus buildStatus)
		{
			return buildStatus switch
			{
				BuildStatus.OutOfDate => UIColors.Negative,
				BuildStatus.InProgress => UIColors.Progress,
				BuildStatus.Comparing => UIColors.Progress,
				_ => UIColors.Positive
			};
		}

		protected override void Draw(double alpha)
		{
			var commandBuffer = GraphicsDevice.AcquireCommandBuffer();
			var swapchainTexture = commandBuffer.AcquireSwapchainTexture(MainWindow);
			if (swapchainTexture != null)
			{
				ImGuiBackend.UploadAndRenderBuffers(
					commandBuffer,
					new ColorTargetInfo(swapchainTexture, Color.White)
				);
			}

			// You must always submit the command buffer.
			GraphicsDevice.Submit(commandBuffer);
		}

		protected override void Destroy()
		{

		}

		public class DebugTextureStorage
		{
			Dictionary<IntPtr, WeakReference<Texture>> PointerToTexture = new Dictionary<IntPtr, WeakReference<Texture>>();

			public IntPtr Add(Texture texture)
			{
				if (!PointerToTexture.ContainsKey(texture.Handle))
				{
					PointerToTexture.Add(texture.Handle, new WeakReference<Texture>(texture));
				}
				return texture.Handle;
			}

			public Texture GetTexture(IntPtr pointer)
			{
				if (!PointerToTexture.ContainsKey(pointer))
				{
					return null;
				}

				var result = PointerToTexture[pointer];

				if (!result.TryGetTarget(out var texture))
				{
					PointerToTexture.Remove(pointer);
					return null;
				}

				return texture;
			}
		}
	}
}
