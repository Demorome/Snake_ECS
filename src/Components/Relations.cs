using System.Numerics;
using MoonTools.ECS;
using RollAndCash.Data;

namespace RollAndCash.Relations;

public readonly record struct Colliding();
public readonly record struct HasScore();
public readonly record struct UpdateDisplayScoreOnDestroy(bool Negative);
public readonly record struct TimingFootstepAudio();
public readonly record struct TeleportToAtTimerEnd(Entity TeleportTo);
public readonly record struct TargetingEntity();
public readonly record struct DeleteWhenTimerEnds();
public readonly record struct TrailingVisuals();
public readonly record struct Source();
public readonly record struct Following(bool MatchPosition, bool LookTowards, bool StretchTowards);
public readonly record struct DontMove();
public readonly record struct DontDraw();
public readonly record struct CountUpScore(int Start, int End);
public readonly record struct DontTime();

public readonly record struct WillFlipHorizontally(float TimePerFlip);
public readonly record struct WillFlipVertically(float TimePerFlip);
public readonly record struct WillRotate(float TimePerRotation, float Angle);

public readonly record struct FlippedHorizontally();
public readonly record struct FlippedVertically();
public readonly record struct Rotated(float Angle);

public readonly record struct SpeedMult(float Value);
public readonly record struct DontFollowTarget();

// Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
public readonly record struct Invincible();
public readonly record struct WillFlicker(float TimePerFlicker); // called "Flickering" in the tutorial

