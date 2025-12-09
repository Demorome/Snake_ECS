
using MoonWorks.Graphics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RollAndCash;

[StructLayout(LayoutKind.Explicit, Size = 48)]
struct PositionTextureColorVertex : IVertexType
{
	[FieldOffset(0)]
	public Vector4 Position;

	[FieldOffset(16)]
	public Vector2 TexCoord;

	[FieldOffset(32)]
	public Vector4 Color;

	public static VertexElementFormat[] Formats { get; } =
	[
		VertexElementFormat.Float4,
		VertexElementFormat.Float2,
		VertexElementFormat.Float4
	];

	public static uint[] Offsets { get; } =
	[
		0,
		16,
		32
	];
}