using MoonTools.ECS;

namespace RollAndCash.Relations;

public readonly record struct Colliding();
public readonly record struct HasScore();
public readonly record struct UpdateDisplayScoreOnDestroy(bool Negative);
public readonly record struct TimingFootstepAudio();
public readonly record struct TeleportToAtTimerEnd(Entity TeleportTo);
public readonly record struct Targeting();
public readonly record struct DontMove();
public readonly record struct DontDraw();
public readonly record struct CountUpScore(int Start, int End);
public readonly record struct DontTime();

// Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
public readonly record struct Invincible();
public readonly record struct Flickering(float TimePerFlicker);
//public readonly record struct Flicker();
