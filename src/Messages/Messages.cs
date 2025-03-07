using RollAndCash.Content;
using RollAndCash.Systems;
using MoonTools.ECS;
using MoonWorks.Audio;
using RollAndCash.Components;
using RollAndCash.Data;
using System.Numerics;

namespace RollAndCash.Messages;

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

// Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
public readonly record struct Shoot(Entity Source, CollisionLayer Layer, Vector2 Direction, float Speed);