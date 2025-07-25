using System;
using System.Numerics;

namespace RollAndCash;

public struct UV : IEquatable<UV>
{
	public Vector2 Position { get; }
	public Vector2 Dimensions { get; }

	public float Top { get; }
	public float Left { get; }
	public float Bottom { get; }
	public float Right { get; }

	public Vector2 LeftTop { get; }
	public Vector2 RightTop { get; }
	public Vector2 LeftBottom { get; }
	public Vector2 RightBottom { get; }

	public Vector4 Rect { get; }

	public UV(Vector2 position, Vector2 dimensions)
	{
		Position = position;
		Dimensions = dimensions;

		Left = position.X;
		Top = position.Y;
		Right = position.X + dimensions.X;
		Bottom = position.Y + dimensions.Y;

		LeftTop = new Vector2(Left, Top);
		RightTop = new Vector2(Right, Top);
		LeftBottom = new Vector2(Left, Bottom);
		RightBottom = new Vector2(Right, Bottom);

		Rect = new Vector4(Left, Top, Right, Bottom);
	}

	public override bool Equals(object obj)
	{
		return obj is UV && Equals((UV)obj);
	}
	public bool Equals(UV otherUV)
	{
		return Rect == otherUV.Rect;
	}
	public static bool operator ==(UV uv1, UV uv2)
	{
		return uv1.Equals(uv2);
	}
	public static bool operator !=(UV uv1, UV uv2)
	{
		return !uv1.Equals(uv2);
	}
    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
