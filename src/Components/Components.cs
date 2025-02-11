using MoonWorks.Graphics;
using Snake.Systems;
using Snake.Data;
using Snake.Messages;
using System.Numerics;

namespace Snake.Components;

public readonly record struct Rectangle(int X, int Y, int Width, int Height);
public readonly record struct DrawAsRectangle();

public readonly record struct PlayerIndex(int Index);
//public readonly record struct MaxSpeed(float Value);

public readonly record struct MovementTimer(float TimeLeftInSecs, float Max)
{
    public float PercentRemaining => TimeLeftInSecs / Max;
    public MovementTimer(float time) : this(time, time) { }
}
public readonly record struct LastMovedDirection(Vector2 Direction);
public readonly record struct CachedDirection(Vector2 Direction);
public readonly record struct IntegerVelocity(Vector2 Value);


public readonly record struct Solid();
public readonly record struct TouchingSolid();
public readonly record struct Wall();


public readonly record struct Score(int Value);
public readonly record struct DisplayScore(int Value);
public readonly record struct ColorBlend(Color Color);
public readonly record struct ColorSpeed(float RedSpeed, float GreenSpeed, float BlueSpeed);

public readonly record struct ColorFlicker(int ElapsedFrames, Color Color);
public readonly record struct Depth(float Value);
public readonly record struct SpriteScale(float Scale);

public readonly record struct IsScoreScreen(); // sorry
public readonly record struct TextDropShadow(int OffsetX, int OffsetY);
public readonly record struct GameInProgress(); // yaaargh

public readonly record struct DirectionalSprites(
    SpriteAnimationInfoID Up,
    SpriteAnimationInfoID Right,
    SpriteAnimationInfoID Down,
    SpriteAnimationInfoID Left
    );