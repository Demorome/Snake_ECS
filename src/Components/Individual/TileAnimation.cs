/*using System;
using System.Numerics;
using RollAndCash.Data;
using MoonWorks.Math;

namespace RollAndCash.Components;

public readonly record struct TileAnimation : IAnimation
{
	//== IAnimation stuff.
	public readonly int FrameRate { get; }
	public readonly bool Loop { get; }
	public readonly Vector2 Origin { get; }
	public readonly float RawFrameIndex { get; }
	public readonly int FramesLength 
	{ 
		get
        {
            return SpriteAnimationInfo.Frames.Length;
        } 
	}

	//== Our own members.
	public SpriteAnimationInfoID SpriteAnimationInfoID { get; }

	//== Properties & methods.
	public SpriteAnimationInfo SpriteAnimationInfo 
		=> SpriteAnimationInfo.FromID(SpriteAnimationInfoID)
	;
	public int FrameIndex
	{
		get
        {
            return IAnimation.GetFrameIndex(this);
        }
	}
	public Sprite CurrentSprite => SpriteAnimationInfo.Frames[FrameIndex];
	public bool Finished => IAnimation.IsFinished(this);
	public float TotalTime => IAnimation.GetTotalTime(this);

	public int TimeOf(int frame)
	{
		return IAnimation.GetTimeOf(this, frame);
	}

	// FIXME: this isn't really necessary
	public static SpriteAnimation ForceFrame(
		SpriteAnimationInfo spriteAnimationInfo,
		int frameIndex
	)
	{
		return new SpriteAnimation(
			spriteAnimationInfo,
			0,
			false,
			frameIndex
		);
	}

	public SpriteAnimation ChangeFramerate(int frameRate)
	{
		return new SpriteAnimation(
			SpriteAnimationInfo,
			frameRate,
			Loop,
			RawFrameIndex,
			Origin);
	}
	public SpriteAnimation ChangeLoops(bool loops)
	{
		return new SpriteAnimation(
			SpriteAnimationInfo,
			FrameRate,
			loops,
			RawFrameIndex,
			Origin);
	}
	public SpriteAnimation ChangeOrigin(Vector2 origin)
	{
		return new SpriteAnimation(
			SpriteAnimationInfo,
			FrameRate,
			Loop,
			RawFrameIndex,
			origin);
	}
	public SpriteAnimation ChangeRawFrameIndex(float rawFrameIndex)
	{
		return new SpriteAnimation(
			SpriteAnimationInfo,
			FrameRate,
			Loop,
			rawFrameIndex,
			Origin);
	}

	//== Constructors.
	public SpriteAnimation(
		SpriteAnimationInfo spriteAnimationInfo
	)
	{
		SpriteAnimationInfoID = spriteAnimationInfo.ID;
		FrameRate = spriteAnimationInfo.FrameRate;
		Loop = true;
		Origin = new Vector2(spriteAnimationInfo.OriginX, spriteAnimationInfo.OriginY);
		RawFrameIndex = 0;
	}

	public SpriteAnimation(
		SpriteAnimationInfo spriteAnimationInfo,
		Vector2 origin
	)
	{
		SpriteAnimationInfoID = spriteAnimationInfo.ID;
		FrameRate = spriteAnimationInfo.FrameRate;
		Loop = false;
		Origin = origin;
		RawFrameIndex = 0;
	}

	public SpriteAnimation(
		SpriteAnimationInfo spriteAnimationInfo,
		bool loop
	)
	{
		SpriteAnimationInfoID = spriteAnimationInfo.ID;
		FrameRate = spriteAnimationInfo.FrameRate;
		Loop = loop;
		Origin = new Vector2(spriteAnimationInfo.OriginX, spriteAnimationInfo.OriginY);
		RawFrameIndex = 0;
	}

	public SpriteAnimation(
		SpriteAnimationInfo spriteAnimationInfo,
		int frameRate
	)
	{
		SpriteAnimationInfoID = spriteAnimationInfo.ID;
		FrameRate = frameRate;
		Loop = true;
		Origin = new Vector2(spriteAnimationInfo.OriginX, spriteAnimationInfo.OriginY);
		RawFrameIndex = 0;
	}

	public SpriteAnimation(
		SpriteAnimationInfo spriteAnimationInfo,
		int frameRate,
		bool loop
	)
	{
		SpriteAnimationInfoID = spriteAnimationInfo.ID;
		FrameRate = frameRate;
		Loop = loop;
		Origin = new Vector2(spriteAnimationInfo.OriginX, spriteAnimationInfo.OriginY);
		RawFrameIndex = 0;
	}

	public SpriteAnimation(
		SpriteAnimationInfo spriteAnimationInfo,
		int frameRate,
		bool loop,
		int frameIndex
	)
	{
		SpriteAnimationInfoID = spriteAnimationInfo.ID;
		FrameRate = frameRate;
		Loop = loop;
		Origin = new Vector2(spriteAnimationInfo.OriginX, spriteAnimationInfo.OriginY);
		RawFrameIndex = frameIndex;
	}

	public SpriteAnimation(
		SpriteAnimationInfo spriteAnimationInfo,
		int frameRate,
		bool loop,
		float rawFrameIndex
	)
	{
		SpriteAnimationInfoID = spriteAnimationInfo.ID;
		FrameRate = frameRate;
		Loop = loop;
		Origin = new Vector2(spriteAnimationInfo.OriginX, spriteAnimationInfo.OriginY);
		RawFrameIndex = rawFrameIndex;
	}

	public SpriteAnimation(
		SpriteAnimationInfo spriteAnimationInfo,
		int frameRate,
		bool loop,
		float rawFrameIndex,
		Vector2 origin
	)
	{
		SpriteAnimationInfoID = spriteAnimationInfo.ID;
		FrameRate = frameRate;
		Loop = loop;
		Origin = origin;
		RawFrameIndex = rawFrameIndex;
	}

	public SpriteAnimation Update(float dt)
	{
		return new SpriteAnimation(
			SpriteAnimationInfo,
			FrameRate,
			Loop,
			RawFrameIndex + (FrameRate * dt),
			Origin
		);
	}
}
*/