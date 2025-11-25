using MoonWorks.Graphics;
using System.Numerics;
using RollAndCash.Data;
using System.Drawing;

namespace RollAndCash.Components;

// Like a regular sprite, but replacing the TexturePage stuff, since that struct requires Cram to be used.
// We also don't need the FrameRect info, since our TileSets aren't being crammed.
public struct TileSprite
{
	public VisualSetID TileSetID { get; }
	public VisualSetVariantID TileSetVariantID;
	public Vector2 PixelPos { get; } // the pixel position on the texture
	public Vector2 Origin => PixelPos;
    public Vector2 TilePos => new Vector2(PixelPos.X, PixelPos.Y) / TileSize;
	public UV UV { get; }

	public static TileSprite FromID(TileID TileID) => TileSet.GetTileSprite(TileID);
	public TileSet TileSet => TileSet.FromID(TileSetID);
	public Texture Texture => TileSet.GetTextureForVariant(TileSetVariantID);
	public int TileSize => TileSet.TileSize;

	public TileSprite(
		TileSet tileSet,
		Vector2 pixelPos
	)
	{
		TileSetID = tileSet.ID;
		TileSetVariantID = new(0);
		PixelPos = pixelPos;
		UV = new UV(
			new Vector2((float)PixelPos.X / tileSet.PixelWidth, (float)PixelPos.Y / tileSet.PixelHeight),
			new Vector2((float)TileSize / tileSet.PixelWidth, (float)TileSize / tileSet.PixelHeight)
		);
	}
}
