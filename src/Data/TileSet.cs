using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Content;

namespace RollAndCash.Data;

public readonly record struct TileID(
    PositionInVisualSet PosInSet, 
    VisualSetID TileSetID, 
    VisualSetVariantID VariantID = default
    )
{
    public static explicit operator TileID(VisualFromSetID_ForSpawning v)
    {
        return new TileID(v.PosInSet, v.VisualSetID, v.VariantID);
    }
}


public class TileSet : VisualSet
{
    public string FullJsonFilePath { get; private set; }
    public int TileSize = Dimensions.TILE_SIZE;
    public int PixelHeight, PixelWidth;
    public Texture DefaultTexture { get; private set; } = null;

    private TileSprite[,] TileSprites = null;

    public TileSet(string fileName, string fullFilePath) 
        : base(fileName)
    {
        FullJsonFilePath = fullFilePath;
    }

    public static TileSet FromID(VisualSetID id)
    {
        return (TileSet)IDLookup[id.ID];
    }

    public static TileSprite GetTileSprite(TileID TileID)
    {
        var tileSet = (TileSet)IDLookup[TileID.TileSetID.ID];
        var tileSprite = tileSet.TileSprites[TileID.PosInSet.X, TileID.PosInSet.Y];
        tileSprite.TileSetVariantID = TileID.VariantID;
        return tileSprite;
    }

    // Only used when loading a TileSprite as an entity, to set initial ColorBlend.
    /*public static (TileSprite, Color) GetTileSpriteAndColor(TileID TileID)
    {
        var tileSet = (TileSet)IDLookup[TileID.TileSetID.ID];
        var tileSprite = tileSet.TileSprites[TileID.PosInSet.X, TileID.PosInSet.Y];
        tileSprite.TileSetVariantID = TileID.VariantID;

        if (TileID.VariantID.ID == 0)
        {
            return new(tileSprite, Color.White);
        }
        else
        {
            var variantTileSet = tileSet.VariantSets[TileID.VariantID.ID];
            return new(tileSprite, variantTileSet.GetTileColorOverride(TileID.PosInSet));
        }
    }*/

    public Texture GetTextureForVariant(VisualSetVariantID TileSetVariantID)
    {
        if (TileSetVariantID.ID == 0)
        {
            return DefaultTexture;
        }
        else
        {
            return (VariantSets[TileSetVariantID.ID - 1] as TileSetVariant).Texture;
        }
    }

    public void Load(GraphicsDevice graphicsDevice, TileSetAtlasData atlasData)
	{
        TileSize = atlasData.TileSize;
        PixelHeight = atlasData.PixelHeight;
        PixelWidth = atlasData.PixelWidth;
        NumRows = (ushort)(PixelHeight / TileSize);
        NumColumns = (ushort)(PixelWidth / TileSize);

        TileSprites = new TileSprite[NumColumns, NumRows];
		for (int i = 0; i < NumColumns; ++i)
		{
            for (int j = 0; j < NumRows; ++j)
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
        foreach (TileSetVariant variant in VariantSets)
        {
            variant.UnloadUnlessDefaultTexture(DefaultTexture);
        }

		DefaultTexture.Dispose();
		DefaultTexture = null;
	}

    public override Vector2 GetVisualSize(PositionInVisualSet posInVisualSet, VisualSetVariantID variantID)
    {
        return new Vector2(TileSize, TileSize);
    }

#if DEBUG
    public override bool Editor_CanAddOrRemoveVisuals()
    {
        return false;
    }
    public override bool Editor_TrySetColumnCount(ushort newWidth)
    {
        return false;
    }
    public override bool Editor_IsVisualFullyTransparent(
        PositionInVisualSet posInVisualSet, 
        VisualSetVariantID variantID
        )
    {
        // TODO!!!! Detect if a tile is fully empty/transparent -> don't allow drawing it
        return false;
    }

    public static (PrefabID, FiledEntity.Flags, PrefabSpawnInfoOverride?) 
        GetMetadata(TileID tileID)
    {
        return VisualSet.GetMetadata(tileID.PosInSet, tileID.TileSetID, tileID.VariantID);
    }
#endif
}

// Also known as Palette Swaps, though in rare cases it may also have different per-tile metadata.
public class TileSetVariant : VisualSetVariant
{
    // Might be the same as the default TileSet, if we just want to create some tile color variants in-editor.
    // Or if we just want to have other different metadata per tile, 
    // such as as version of a tile that isn't solid for secret walls.
    public Texture Texture { get; private set; } = null;

    public TileSetVariant(Texture texture, TileSet parent) : base(parent)
    {
        Texture = texture;
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