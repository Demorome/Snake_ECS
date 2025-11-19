using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Content;

namespace RollAndCash.Data;

// In non-debug builds, this is only used when instantiating tiles.
// Otherwise, in the debug-mode Editor, tiles can be 'painted' with this metadata.
[Flags]
public enum FiledTileMetadata 
{
    IsAnimated              = 1 << 0,
    // Ex: "IsWater", "IsLava", "IsSpike", "HasUniqueColliderShape", etc.
}

public readonly record struct TileSetID(ushort ID);

// A TileSetVariantID of 0 means using the default TileSet.
public readonly record struct TileSetVariantID(byte ID);

public readonly record struct TileID(ushort TileX, ushort TileY, TileSetID TileSetID, TileSetVariantID VariantID = default);

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
    public Texture DefaultTexture { get; private set; } = null;

#if DEBUG
    public List<List<FiledTileMetadata>> PerTileMetadata = new();

    // TODO: Support per-tile collision shapes, should we need it.
#endif

    private TileSprite[,] TileSprites = null;

    // The default TileSet has a variant ID of 0, but isn't included here.
    // Thus, substract 1 whenever we index into this.
    public List<TileSetVariant> VariantTileSets = new();

    public TileSet(string name)
	{
		lock (IDLookup)
		{
			ID = new TileSetID((ushort)IDLookup.Count);
			IDLookup.Add(this);
		}
		Name = name;
	}

    public static TileSet FromID(TileSetID id)
    {
        return IDLookup[id.ID];
    }

    public static TileSprite GetTileSprite(TileID TileID)
    {
        var tileSet = IDLookup[TileID.TileSetID.ID];
        var tileSprite = tileSet.TileSprites[TileID.TileX, TileID.TileY];
        tileSprite.TileSetVariantID = TileID.VariantID;
        return tileSprite;
    }

    // Only used when loading a TileSprite as an entity, to set initial ColorBlend.
    public static (TileSprite, Color) GetTileSpriteAndColor(TileID TileID)
    {
        var tileSet = IDLookup[TileID.TileSetID.ID];
        var tileSprite = tileSet.TileSprites[TileID.TileX, TileID.TileY];
        tileSprite.TileSetVariantID = TileID.VariantID;

        if (TileID.VariantID.ID == 0)
        {
            return new(tileSprite, Color.White);
        }
        else
        {
            var variantTileSet = tileSet.VariantTileSets[TileID.VariantID.ID];
            return new(tileSprite, variantTileSet.GetTileColorOverride(TileID.TileX, TileID.TileY));
        }
    }

    public Texture GetTextureForVariant(TileSetVariantID TileSetVariantID)
    {
        if (TileSetVariantID.ID == 0)
        {
            return DefaultTexture;
        }
        else
        {
            return VariantTileSets[TileSetVariantID.ID - 1].Texture;
        }
    }

    public void Load(GraphicsDevice graphicsDevice, TileSetAtlasData atlasData)
	{
        TileSize = atlasData.TileSize;
        PixelHeight = atlasData.PixelHeight;
        PixelWidth = atlasData.PixelWidth;
        TileHeight = PixelHeight / TileSize;
        TileWidth = PixelWidth / TileSize;

        TileSprites = new TileSprite[TileWidth, TileHeight];
		for (int i = 0; i < TileWidth; ++i)
		{
            for (int j = 0; j < TileHeight; ++j)
            {
                var pixelPos = new Vector2(i * TileSize, j * TileSize);
                var tileSprite = new TileSprite(this, pixelPos);

                TileSprites[i, j] = tileSprite;
            }
		}

		DefaultTexture = Texture.Create2D(
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
        foreach (var variant in VariantTileSets)
        {
            variant.UnloadUnlessDefaultTexture(DefaultTexture);
        }

		DefaultTexture.Dispose();
		DefaultTexture = null;
	}
}

// Also known as Palette Swaps.
public class TileSetVariant
{
    public TileSetVariantID ID;

    // Might be the same as the default TileSet, if we just want to create some tile color variants in-editor.
    public Texture Texture { get; private set; } = null;
    public List<List<Color>> TileColorOverrides = null;

    public TileSetVariant(Texture texture, List<TileSetVariant> list)
    {
        Texture = texture;
        lock (list)
        {
            ID = new TileSetVariantID((byte)(list.Count + 1));
            list.Add(this);
        }
    }

    public Color GetTileColorOverride(ushort TileX, ushort TileY)
    {
        if (TileColorOverrides != null)
        {
            return TileColorOverrides[TileX][TileY];
        }
        else
        {
            return Color.White;
        }
    }

    public void UnloadUnlessDefaultTexture(Texture DefaultTexture)
    {
        if (Texture.Handle != DefaultTexture.Handle)
        {
            Texture.Dispose();
            Texture = null;
        }
    }
}