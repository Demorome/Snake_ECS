using System;
using System.Numerics;

public readonly record struct Position2D
{
    private readonly Vector2 RawPosition;
    public readonly int X { get; }
    public readonly int Y { get; }

    public Position2D(float x, float y)
    {
        RawPosition = new Vector2(x, y);
        X = (int)MathF.Round(x);
        Y = (int)MathF.Round(y);
    }

    public Position2D(int x, int y)
    {
        RawPosition = new Vector2(x, y);
        X = x;
        Y = y;
    }

    public Position2D(Vector2 v)
    {
        RawPosition = v;
        X = (int)MathF.Round(v.X);
        Y = (int)MathF.Round(v.Y);
    }

    public Position2D SetX(int x)
    {
        return new Position2D((float)x, RawPosition.Y);
    }

    public Position2D SetY(int y)
    {
        return new Position2D(RawPosition.X, (float)y);
    }

    public static Position2D operator +(Position2D a, Position2D b)
    {
        return new Position2D(a.RawPosition + b.RawPosition);
    }

    public static Vector2 operator -(Position2D a, Position2D b)
    {
        return a.RawPosition - b.RawPosition;
    }

    public static Position2D operator +(Position2D a, Vector2 b)
    {
        return new Position2D(a.RawPosition + b);
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }

    public Vector2 AsVector()
    {
        return new Vector2(X, Y);
    }

}

public readonly record struct LastPosition(Vector2 Value);