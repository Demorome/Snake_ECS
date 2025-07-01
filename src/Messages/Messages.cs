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
public readonly record struct DealDamage(Entity Target, int Damage);
public readonly record struct Collide(Entity A, Entity B);
public readonly record struct StartFlickering(Entity Target, float TotalTime, float FlickerTime);