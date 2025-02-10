using MoonWorks.Graphics;
using RollAndCash.Systems;
using RollAndCash.Data;
using RollAndCash.Messages;

namespace RollAndCash.Components;

public readonly record struct Player(int Index);
public readonly record struct Solid();
public readonly record struct TouchingSolid();
public readonly record struct Score(int Value);
public readonly record struct DisplayScore(int Value);
public readonly record struct ColorBlend(Color Color);

public readonly record struct Depth(float Value);
public readonly record struct DrawAsRectangle();

public readonly record struct LastDirection(System.Numerics.Vector2 Direction);
public readonly record struct IsScoreScreen(); // sorry
public readonly record struct GameInProgress(); // yaaargh

public readonly record struct DirectionalSprites(
    SpriteAnimationInfoID Up,
    SpriteAnimationInfoID Right,
    SpriteAnimationInfoID Down,
    SpriteAnimationInfoID Left
    );