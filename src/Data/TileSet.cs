using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Content;

namespace RollAndCash.Data;

public readonly record struct TileSetID(int ID);

public class TileSet
{
	static List<TileSet> IDLookup = new();
	//static Dictionary<string, TileSet> TileSetsByName = new();

    public string Name;
    public readonly string JsonFilePath;
    public TileSetID ID;
    public int TileSize = Dimensions.TILE_SIZE;
    public int PixelHeight, PixelWidth;
    public int TileHeight, TileWidth;
    public Texture Texture { get; private set; } = null;

    private List<List<TileSprite>> TileSprites = null;

    public TileSet(string name)
	{
		lock (IDLookup)
		{
			ID = new TileSetID(IDLookup.Count);
			IDLookup.Add(this);
		}
		Name = name;
	}

    public static TileSet FromID(TileSetID id)
    {
        return IDLookup[id.ID];
    }

    public TileSprite GetTileSprite(int TileX, int TileY)
    {
        return TileSprites[TileX][TileY];
    }

    public void Load(GraphicsDevice graphicsDevice, TileSetAtlasData atlasData)
	{
        TileSize = atlasData.TileSize;
        PixelHeight = atlasData.PixelHeight;
        PixelWidth = atlasData.PixelWidth;
        TileHeight = PixelHeight / TileSize;
        TileWidth = PixelWidth / TileSize;

        TileSprites = new(TileWidth);
		for (int i = 0; i < TileWidth; ++i)
		{
            TileSprites.Add(new List<TileSprite>(TileHeight));
            for (int j = 0; j < TileHeight; ++j)
            {
                var pixelPos = new Vector2(i * TileSize, j * TileSize);
                var tileSprite = new TileSprite(this, pixelPos);

                TileSprites[i].Add(tileSprite);
            }
		}

		Texture = Texture.Create2D(
			graphicsDevice,
			atlasData.Name,
            (uint)atlasData.PixelWidth,
            (uint)atlasData.PixelHeight,
			TextureFormat.R8G8B8A8Unorm,
			TextureUsageFlags.Sampler
		);
	}

	/*public void LoadImage(GraphicsDevice graphicsDevice, ReadOnlySpan<byte> data)
	{
		var resourceUploader = new ResourceUploader(graphicsDevice);
		resourceUploader.SetTextureDataFromCompressed(new TextureRegion(Texture), data);
		resourceUploader.Upload();
		resourceUploader.Dispose();
	}*/

	private void Unload()
	{
		Texture.Dispose();
		Texture = null;
	}
}