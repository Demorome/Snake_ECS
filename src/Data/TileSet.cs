using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Content;

namespace RollAndCash.Data;

public readonly record struct TileSetID(ushort ID);

// A TileSetVariantID of 0 means using the default TileSet.
public readonly record struct TileSetVariantID(byte ID);
public readonly record struct PositionInVisualSet(ushort X, ushort Y);

public readonly record struct TileID(PositionInVisualSet PosInSet, TileSetID TileSetID, TileSetVariantID VariantID = default);

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

    // So that we can automatically assign a PrefabID, flags & extradata to certain tiles when we spawn them.
    // Tiles not contained here must automatically be purely visual with nothing special going on.
    public Dictionary<PositionInVisualSet, 
        (PrefabID, FiledEntity.Flags, FiledEntity.ExtraSpawnInfo?)> 
        TileMetadata = new();

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
        var tileSprite = tileSet.TileSprites[TileID.PosInSet.X, TileID.PosInSet.Y];
        tileSprite.TileSetVariantID = TileID.VariantID;
        return tileSprite;
    }

    public static (PrefabID, FiledEntity.Flags, FiledEntity.ExtraSpawnInfo?) 
        GetTileMetadata(TileID TileID)
    {
        var tileSet = IDLookup[TileID.TileSetID.ID];

        (PrefabID, FiledEntity.Flags, FiledEntity.ExtraSpawnInfo?) result;
        if (tileSet.TileMetadata.ContainsKey(TileID.PosInSet))
        {
            result = tileSet.TileMetadata[TileID.PosInSet];
        }
        else
        {
            result = (new PrefabID(Prefabs.VisualTile), FiledEntity.Flags.None, null);
        }

        if (TileID.VariantID.ID != 0)
        {
            var variantTileSet = tileSet.VariantTileSets[TileID.VariantID.ID];
            var maybeMetadataOverride = variantTileSet.GetTileTileMetadataOverride(TileID.PosInSet);
            if (maybeMetadataOverride.HasValue)
            {
                if (maybeMetadataOverride.Value.Item1.HasValue)
                {
                    result.Item1 = maybeMetadataOverride.Value.Item1.Value;
                }
                if (maybeMetadataOverride.Value.Item2.HasValue)
                {
                    result.Item2 = maybeMetadataOverride.Value.Item2.Value;
                }
                if (maybeMetadataOverride.Value.Item3 != null)
                {
                    result.Item3 = maybeMetadataOverride.Value.Item3;
                }
            }
        }
        return result;
    }

    // Only used when loading a TileSprite as an entity, to set initial ColorBlend.
    public static (TileSprite, Color) GetTileSpriteAndColor(TileID TileID)
    {
        var tileSet = IDLookup[TileID.TileSetID.ID];
        var tileSprite = tileSet.TileSprites[TileID.PosInSet.X, TileID.PosInSet.Y];
        tileSprite.TileSetVariantID = TileID.VariantID;

        if (TileID.VariantID.ID == 0)
        {
            return new(tileSprite, Color.White);
        }
        else
        {
            var variantTileSet = tileSet.VariantTileSets[TileID.VariantID.ID];
            return new(tileSprite, variantTileSet.GetTileColorOverride(TileID.PosInSet));
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

    // FIXME: Auto-unload when this TileSet is disposed?
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

// Also known as Palette Swaps, though in rare cases it may also have different per-tile metadata.
public class TileSetVariant
{
    public TileSetVariantID ID;

#if DEBUG
    // Only used as an optional describer for the Editor.
    public string Name;
#endif

    // Might be the same as the default TileSet, if we just want to create some tile color variants in-editor.
    // Or if we just want to have other different metadata per tile, such as as version of a tile that isn't solid for secret walls.
    public Texture Texture { get; private set; } = null;

    public List<List<Color>> TileColorOverrides = null;

    // NOTE: If any field is non-null, it completely overrides the base field (like Flags).
    public Dictionary<PositionInVisualSet, 
        (PrefabID?, FiledEntity.Flags?, FiledEntity.ExtraSpawnInfo?)>  
        TileMetadataOverrides = null;

    public TileSetVariant(Texture texture, List<TileSetVariant> list)
    {
        Texture = texture;
        lock (list)
        {
            ID = new TileSetVariantID((byte)(list.Count + 1));
            list.Add(this);
        }
    }

    public Color GetTileColorOverride(PositionInVisualSet tilePosInSet)
    {
        if (TileColorOverrides != null)
        {
            return TileColorOverrides[tilePosInSet.X][tilePosInSet.Y];
        }
        else
        {
            return Color.White;
        }
    }

    public (PrefabID?, FiledEntity.Flags?, FiledEntity.ExtraSpawnInfo?)? 
        GetTileTileMetadataOverride(PositionInVisualSet tilePosInSet)
    {
        if (TileMetadataOverrides != null)
        {
            if (TileMetadataOverrides.ContainsKey(tilePosInSet))
            {
                return TileMetadataOverrides[tilePosInSet];
            }
        }
        return null;
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