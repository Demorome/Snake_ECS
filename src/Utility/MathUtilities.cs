using System;
using System.Numerics;

namespace RollAndCash.Utility;

public static class MathUtilities
{
    public static Vector2 SafeNormalize(Vector2 v)
    {
        if (v.LengthSquared() == 0)
        {
            return Vector2.Zero;
        }

        return Vector2.Normalize(v);
    }

    /// <summary>
    /// Avoid using this repeatedly; cache the rotation matrix and use that overload instead.
    /// </summary>
    public static Vector2 Rotate(Vector2 vector, float rotationInRadians)
    {
        return Vector2.TransformNormal(vector, Matrix4x4.CreateRotationZ(rotationInRadians));
    }
    public static Vector2 Rotate(Vector2 vector, Matrix4x4 rotationMatrixZ)
    {
        return Vector2.TransformNormal(vector, rotationMatrixZ);
    }

    public static Matrix4x4 GetRotationMatrix(float rotationInRadians)
    {
        return Matrix4x4.CreateRotationZ(rotationInRadians);
    }

    // In radians
    // FIXME: Does this need to be SafeNormalized?
    public static float GetHeadingAngle(Vector2 origin, Vector2 target)
    {
        var orientation = MathF.Atan2(target.Y - origin.Y, target.X - origin.X);
        return orientation;
    }

    public static Vector2 GetHeadingUnitVector(Vector2 origin, Vector2 target)
    {
        return UnitVectorFromAngle(
            GetHeadingAngle(origin, target)
        );
    }

    // In radians
    public static float AngleFromUnitVector(Vector2 unitVector)
    {
        return MathF.Atan2(unitVector.Y, unitVector.X);

        /*
        // Credits to sandolkakos and TheDarkVoid: https://discussions.unity.com/t/calculating-the-angle-of-a-vector2-from-zero/69663

        if (unitVector == Vector2.Zero)
        {
            return 0.0f;
        }

        var angle = MathF.Atan2(unitVector.Y, unitVector.X) * MathF.Sign(unitVector.X);
        if (unitVector.Y < 0)
        {

        }
        return angle;*/
    }

    public static Vector2 UnitVectorFromAngle(float angleInRadians)
    {
        return SafeNormalize(
            new Vector2(
                MathF.Cos(angleInRadians), 
                MathF.Sin(angleInRadians)
            )
        );
    }
}
