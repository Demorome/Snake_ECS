using System;
using System.Numerics;

// WARN: Anyone setting this MUST make sure to update PixelPosition as well.
public readonly record struct TilePosition
{
    public readonly Vector2 Position;
    public readonly int X { get; }
    public readonly int Y { get; }

    public TilePosition(int x, int y)
    {
        Position = new Vector2(x, y);
        X = x;
        Y = y;
    }

    public TilePosition(Vector2 v)
    {
        X = (int)MathF.Round(v.X);
        Y = (int)MathF.Round(v.Y);
        Position = new Vector2(X, Y);
    }

    public TilePosition SetX(int x)
    {
        return new TilePosition(x, (int)Position.Y);
    }

    public TilePosition SetY(int y)
    {
        return new TilePosition((int)Position.X, y);
    }

    public static TilePosition operator +(TilePosition a, TilePosition b)
    {
        return new TilePosition(a.Position + b.Position);
    }

    public static Vector2 operator -(TilePosition a, TilePosition b)
    {
        return a.Position - b.Position;
    }

    public static TilePosition operator +(TilePosition a, Vector2 b)
    {
        return new TilePosition(a.Position + b);
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }

}
