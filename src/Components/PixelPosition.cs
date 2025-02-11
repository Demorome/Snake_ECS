using System;
using System.Numerics;

public readonly record struct PixelPosition
{
    private readonly Vector2 RawPosition;
    public readonly int X { get; }
    public readonly int Y { get; }

    public PixelPosition(float x, float y)
    {
        RawPosition = new Vector2(x, y);
        X = (int)MathF.Round(x);
        Y = (int)MathF.Round(y);
    }

    public PixelPosition(int x, int y)
    {
        RawPosition = new Vector2(x, y);
        X = x;
        Y = y;
    }

    public PixelPosition(Vector2 v)
    {
        RawPosition = v;
        X = (int)MathF.Round(v.X);
        Y = (int)MathF.Round(v.Y);
    }

    public PixelPosition SetX(int x)
    {
        return new PixelPosition((float)x, RawPosition.Y);
    }

    public PixelPosition SetY(int y)
    {
        return new PixelPosition(RawPosition.X, (float)y);
    }

    public static PixelPosition operator +(PixelPosition a, PixelPosition b)
    {
        return new PixelPosition(a.RawPosition + b.RawPosition);
    }

    public static Vector2 operator -(PixelPosition a, PixelPosition b)
    {
        return a.RawPosition - b.RawPosition;
    }

    public static PixelPosition operator +(PixelPosition a, Vector2 b)
    {
        return new PixelPosition(a.RawPosition + b);
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }

}
