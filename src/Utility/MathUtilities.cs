using System;
using System.Numerics;

namespace Snake.Utility;

public static class MathUtilities
{
    public static Vector2 SafeNormalize(Vector2 v)
    {
        if (v.LengthSquared() == 0) {
            return Vector2.Zero;
        }

        return Vector2.Normalize(v);
    }

    public static Vector2 Rotate(Vector2 vector, float rotation)
    {
        return Vector2.TransformNormal(vector, Matrix4x4.CreateRotationZ(rotation));
    }

    public static int GetManhattanDistance(Vector2 start, Vector2 target)
    {
        return (int)(Math.Abs(target.X - start.X) + Math.Abs(target.Y - start.Y));
    }
}
