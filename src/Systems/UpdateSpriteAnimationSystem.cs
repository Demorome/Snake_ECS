using System;
using Snake.Components;
using MoonTools.ECS;
using Snake.Relations;
using MoonWorks.Math;
using Snake.Content;
using MoonWorks.Graphics.Font;
using Snake.Messages;
using Snake.Utility;

namespace Snake.Systems;

public class UpdateSpriteAnimationSystem : MoonTools.ECS.System
{
	Filter SpriteAnimationFilter;
	Filter SlowDownAnimationFilter;
	Filter FlickerFilter;
	Filter TextFilter;

	public UpdateSpriteAnimationSystem(World world) : base(world)
	{
		SpriteAnimationFilter = FilterBuilder
			.Include<SpriteAnimation>()
			.Include<PixelPosition>()
			.Build();
		FlickerFilter = FilterBuilder.Include<ColorFlicker>().Build();
		TextFilter = FilterBuilder.Include<Text>().Build();
	}

	public override void Update(TimeSpan delta)
	{
		foreach (var entity in SpriteAnimationFilter.Entities)
		{
			UpdateSpriteAnimation(entity, (float)delta.TotalSeconds);
		}

		// Flicker
		foreach (var entity in FlickerFilter.Entities)
		{
			var flicker = Get<ColorFlicker>(entity);
			var frames = flicker.ElapsedFrames + 1;
			Set(entity, new ColorFlicker(frames, flicker.Color));
		}

		// Score screen text
		foreach (var entity in TextFilter.Entities)
		{
			/*
			if (HasOutRelation<CountUpScore>(entity) && !HasOutRelation<DontDraw>(entity))
			{
				var timerEntity = OutRelationSingleton<CountUpScore>(entity);
				var timeFactor = 1 - Get<Timer>(timerEntity).Remaining;
				var data = GetRelationData<CountUpScore>(entity, timerEntity);
				var value = (int)Math.Floor(float.Lerp(data.Start, data.End, Easing.InOutExpo(timeFactor)));
				Set(entity, new Text(
					Fonts.KosugiID,
					FontSizes.SCORE,
					$"{value}",
					HorizontalAlignment.Center,
					VerticalAlignment.Middle
				));

				var lastValue = Get<LastValue>(entity).value;
				if (lastValue != value)
				{
					Send(new PlayStaticSoundMessage(Rando.GetRandomItem(AudioArrays.Coins), Data.SoundCategory.Generic, 1f, .9f + (.1f * ((float)value / 1000f)), 0f));
				}
			}*/
		}
	}

	public void UpdateSpriteAnimation(Entity entity, float dt)
	{
		var spriteAnimation = Get<SpriteAnimation>(entity).Update(dt);
		Set(entity, spriteAnimation);

		if (spriteAnimation.Finished)
		{
			/*
			if (Has<DestroyOnAnimationFinish>(entity))
			{
				Destroy(entity);
			}
			*/
		}
	}
}
