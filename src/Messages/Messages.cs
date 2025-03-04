using Snake.Content;
using Snake.Systems;
using MoonTools.ECS;
using MoonWorks.Audio;
using Snake.Components;
using Snake.Data;
using System.Numerics;

namespace Snake.Messages;

public readonly record struct PlayStaticSoundMessage(
	StaticSoundID StaticSoundID,
	SoundCategory Category = SoundCategory.Generic,
	float Volume = 1,
	float Pitch = 0,
	float Pan = 0
)
{
	public AudioBuffer Sound => StaticAudio.Lookup(StaticSoundID);
}

public readonly record struct SetAnimationMessage(
	Entity Entity,
	SpriteAnimation Animation,
	bool ForceUpdate = false
);

public readonly record struct PlaySongMessage();
public readonly record struct PlayTitleMusic();
public readonly record struct EndGame();
public readonly record struct GrowActor(
	Entity WhichActor, 
	int Amount = 1);
public readonly record struct DoMovementMessage(
	Entity Entity,
	Vector2 Velocity
);
public readonly record struct DoMovementFirstMessage(
	Entity Entity,
	Vector2 Velocity
);
public readonly record struct SpawnEnemy(
	Vector2 Position,
	int NumTailParts = 0
);
public readonly record struct AdvancedEnemySpawningStage(Entity ToChange, int NewStage)
{
	public int OldStage => NewStage - 1;
};