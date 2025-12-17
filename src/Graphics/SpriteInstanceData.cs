
using Hexa.NET.ImGui;
using MoonTools.ECS;
using MoonWorks.Graphics;
using RollAndCash;
using RollAndCash.Utility;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Rendering;

[StructLayout(LayoutKind.Explicit, Size = 80)]
public readonly record struct SpriteInstanceData
{
	public SpriteInstanceData(
		Vector3 position,
		float rotation,
		Vector2 size,
		Color color,
		Vector2 leftTopUV,
		Vector2 dimensionsUV
	)
    {
		Translation = position;
		Rotation = rotation;
		Scale = size;
		Color = color.ToVector4();

        var left = leftTopUV.X;
		var top = leftTopUV.Y;
		var right = leftTopUV.X + dimensionsUV.X;
		var bottom = leftTopUV.Y + dimensionsUV.Y;

		UV0 = leftTopUV;
		UV1 = new Vector2(right, top);
		UV2 = new Vector2(left, bottom);
		UV3 = new Vector2(right, bottom);
    }

	[FieldOffset(0)]
	public readonly Vector3 Translation;
	[FieldOffset(12)]
	public readonly float Rotation;
	[FieldOffset(16)]
	public readonly Vector2 Scale;
	[FieldOffset(32)]
	public readonly Vector4 Color;
	[FieldOffset(48)]
	public readonly Vector2 UV0;
	[FieldOffset(56)]
	public readonly Vector2 UV1;
	[FieldOffset(64)]
	public readonly Vector2 UV2;
	[FieldOffset(72)]
	public readonly Vector2 UV3;

#if DEBUG
	// We discard Translation data, since we don't want our rendering 
	// to be affected by the sprite's Origin offset.		
	public ImGuiRenderInfo ToImGuiRenderInfo(Vector2 centerPos)
    {
		// Credits to @ocornut for this (slightly tweaked) rotation code: 
		// https://github.com/ocornut/imgui/issues/1982
		var rotationMatrix = MathUtilities.GetRotationMatrix(Rotation);

		// FIXME: Does this still work with negative scale values, to represent flipping?
		Vector2[] pos = 
		{
			centerPos + MathUtilities.Rotate(new Vector2(-Scale.X, -Scale.Y) * 0.5f, rotationMatrix),
			centerPos + MathUtilities.Rotate(new Vector2(+Scale.X, -Scale.Y) * 0.5f, rotationMatrix),
			centerPos + MathUtilities.Rotate(new Vector2(+Scale.X, +Scale.Y) * 0.5f, rotationMatrix),
			centerPos + MathUtilities.Rotate(new Vector2(-Scale.X, +Scale.Y) * 0.5f, rotationMatrix)
		};

		// To avoid bounds checking.
		var posSpan = pos.AsSpan();

		return new ImGuiRenderInfo(
			posSpan[0], posSpan[1], posSpan[2], posSpan[3],
			// Yes, UV3 and UV2 are inverted on purpose.
			// It seems ImGui has a different standard for how to order 
			// UVs than we do, because without doing this,
			// the sprite renders stretched/tilted.
			UV0, UV1, UV3, UV2, 
			ImGui.GetColorU32(Color)
		);
    }
#endif
}

#if DEBUG
public readonly struct ImGuiRenderInfo()
{
	public readonly Vector2 Pos1, Pos2, Pos3, Pos4; // For P1, P2, P3, P4 args
	public readonly Vector2 UV1, UV2, UV3, UV4;
	public readonly uint Color;

	public ImGuiRenderInfo(
		Vector2 pos1, Vector2 pos2, Vector2 pos3, Vector2 pos4,
		Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4,
		uint packedColor) : this()
    {
		Pos1 = pos1; Pos2 = pos2; Pos3 = pos3; Pos4 = pos4;
		UV1 = uv1; UV2 = uv2; UV3 = uv3; UV4 = uv4;
		Color = packedColor;
    }
}
#endif