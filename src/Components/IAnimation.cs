using System;
using System.Numerics;
using RollAndCash.Data;
using MoonWorks.Math;

namespace RollAndCash.Components;

/// <summary>
/// Shared interface for TileAnimation and SpriteAnimation.
/// <para>DO NOT cast them to IAnimation! That incurs a boxing operation. 
/// Use generic functions instead.</para>
/// </summary>
public interface IAnimation
{
    public int FrameRate { get; }
	public bool Loop { get; }
	public Vector2 Origin { get; }
	public float RawFrameIndex { get; }
	public int FramesLength { get; }

	public static int GetTimeOf<T>(T anim, int frame) where T: IAnimation
	{
		return frame / anim.FrameRate;
	}

	// FIXME: should we cache this?
	public static int GetFrameIndex<T>(T anim) where T: IAnimation
	{
		var integerIndex = (int)(MathF.Sign(anim.RawFrameIndex) 
			* MathF.Ceiling(MathF.Abs(anim.RawFrameIndex)))
		;
		var framesLength = anim.FramesLength;
		if (anim.Loop)
		{
			return ((integerIndex % framesLength) + framesLength) % framesLength;
		}
		else
		{
			return int.Clamp(integerIndex, 0, framesLength - 1);
		}
	}

	public static bool IsFinished<T>(T anim) where T: IAnimation => 
		!anim.Loop 
		&& (anim.FrameRate != 0)
		&& (anim.RawFrameIndex >= (anim.FramesLength - 1))
	;

	public static float GetTotalTime<T>(T anim) where T: IAnimation
		=> anim.FramesLength / anim.FrameRate;
}