namespace Snake;

using System.Numerics;

public static class GridInfo
{
    public const int Width = 10;
    public const int Height = 10;
    // Adding 2 to each dimension to account for outer wall layer, which will have Wall entities.
    public const int WidthWithWalls = Width + 2;
    public const int HeightWithWalls = Height + 2;

    // TODO: Pixel dimension stuff

    // Width/height of each cell in the grid.
    public const int PixelCellSize = 32;

    // Thickness of the cell borders.
    public const int PixelBorderSize = 2;

    public static Vector2 TilePositionToPixelPosition(Vector2 tilePos)
    {
        // Todo: account for PixelBorderSize?
        return PixelCellSize * tilePos;
    }
}