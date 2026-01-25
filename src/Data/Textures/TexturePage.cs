using System;
using System.Collections.Generic;
using System.IO;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Storage;
using RollAndCash.Components;
using RollAndCash.Content;

namespace RollAndCash.Data;

public readonly record struct TexturePageID(int ID);

// TODO: Make this Disposable
public class TexturePage
{
	public readonly static List<TexturePage> IDLookup 
		= new List<TexturePage>();

	public string PartialJsonFilePath { get; private set; }

	public string PartialImageFilePath
		=> Path.ChangeExtension(PartialJsonFilePath, ".png");
	//FIXME: Remove this when we can always load w/ TitleStorage!
	public string FullImageFilePath => Path.Combine(
		System.AppContext.BaseDirectory,
		PartialImageFilePath
	);

	public readonly TexturePageID ID;
	public CramTextureAtlasData AtlasData { get; private set;}
	public Texture? Texture { get; private set; } = null;
	public uint Width => (uint)AtlasData.Width;
	public uint Height => (uint)AtlasData.Height;

	private Dictionary<string, Sprite>? Sprites;
	private Dictionary<string, SpriteAnimationInfo>? AnimationInfos;

	public static TexturePage FromID(TexturePageID id)
	{
		return IDLookup[id.ID];
	}

	public TexturePage(string partialJsonFilePath)
	{
		lock (IDLookup)
		{
			ID = new TexturePageID(IDLookup.Count);
			IDLookup.Add(this);
		}
		PartialJsonFilePath = partialJsonFilePath;
	}

	public void ReLoadAtlasInfo(
		GraphicsDevice graphicsDevice, 
		CramTextureAtlasData atlasData)
	{
		AtlasData = atlasData;

		Sprites = new();
		foreach (var image in AtlasData.Images)
		{
			AddSprite(image);
		}

		AnimationInfos = new();
		foreach (var (name, spriteAnimation) in AtlasData.Animations)
		{
			var frames = new List<Sprite>();

			foreach (var frame in spriteAnimation.Frames)
			{
				frames.Add(GetSprite(frame));
			}

			var spriteAnimationInfo = new SpriteAnimationInfo(
				name,
				frames.ToArray(),
				spriteAnimation.FrameRate,
				spriteAnimation.XOrigin,
				spriteAnimation.YOrigin
			);

			AnimationInfos.Add(name, spriteAnimationInfo);
		}

		// Avoid creating a new texture if current one has identical size.
        // This logic only exists to handle the case of asset hot-reloading.
        bool generateTexture = true;

        if (Texture != null)
        {
            if (Texture.Height != (uint)atlasData.Height
                || Texture.Width != (uint)atlasData.Width)
            {
                Texture.Dispose();
            }
            else
            {
                generateTexture = false;
            }
		}

		if (generateTexture)
		{
			Texture = Texture.Create2D(
				graphicsDevice,
				atlasData.Name,
				(uint)AtlasData.Width,
				(uint)AtlasData.Height,
				TextureFormat.R8G8B8A8Unorm,
				TextureUsageFlags.Sampler
			);
		}
	}

#if DEBUG
	public bool Debug_HotReloadAtlasImage(
		GraphicsDevice graphicsDevice, 
		string compressedImagePartialPath,
		TitleStorage storage)
	{
		var resourceUploader = new ResourceUploader(graphicsDevice);

		Texture?.Dispose();
		Texture = resourceUploader.CreateTexture2DFromCompressed(
			storage,
			compressedImagePartialPath,
			TextureFormat.R8G8B8A8Unorm,
			TextureUsageFlags.Sampler
		);

		if (Texture != null)
		{
			resourceUploader.Upload();
		}
		resourceUploader.Dispose();

		return Texture != null;
	}
#endif

	private void Unload()
	{
		Texture?.Dispose();
		Texture = null;
	}

	private void AddSprite(CramTextureAtlasImageData imageData)
	{
		var sliceRect = new Rect
		{
			X = imageData.X,
			Y = imageData.Y,
			W = imageData.W,
			H = imageData.H
		};
		var frameRect = new Rect
		{
			X = imageData.TrimOffsetX,
			Y = imageData.TrimOffsetY,
			W = imageData.UntrimmedWidth,
			H = imageData.UntrimmedHeight
		};
		var sprite = new Sprite(this, sliceRect, frameRect);

		Sprites!.Add(imageData.Name, sprite);
	}

	public SpriteAnimationInfo GetSpriteAnimationInfo(string name)
	{
		return AnimationInfos![name];
	}

	public Sprite GetSprite(string name)
	{
		return Sprites![name];
	}
}
