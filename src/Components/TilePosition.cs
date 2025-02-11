using System;
using System.Numerics;

// WARN: Anyone setting this MUST make sure to update PixelPosition as well.
public readonly record struct TilePosition
{
    public readonly Vector2 PositionVector;
    public readonly int X { get; }
    public readonly int Y { get; }

    public TilePosition(int x, int y)
    {
        PositionVector = new Vector2(x, y);
        X = x;
        Y = y;
    }

    public TilePosition(Vector2 v)
    {
        X = (int)MathF.Round(v.X);
        Y = (int)MathF.Round(v.Y);
        PositionVector = new Vector2(X, Y);
    }

    public TilePosition SetX(int x)
    {
        return new TilePosition(x, (int)PositionVector.Y);
    }

    public TilePosition SetY(int y)
    {
        return new TilePosition((int)PositionVector.X, y);
    }

    public static TilePosition operator +(TilePosition a, TilePosition b)
    {
        return new TilePosition(a.PositionVector + b.PositionVector);
    }

    public static Vector2 operator -(TilePosition a, TilePosition b)
    {
        return a.PositionVector - b.PositionVector;
    }

    public static TilePosition operator +(TilePosition a, Vector2 b)
    {
        return new TilePosition(a.PositionVector + b);
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }

}
