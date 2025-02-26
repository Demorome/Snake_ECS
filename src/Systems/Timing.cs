using System;
using MoonTools.ECS;
using Snake.Relations;
using Snake.Components;
using Snake.Messages;
using Timer = Snake.Components.Timer;
using System.Numerics;

namespace Snake.Systems;

public class Timing : MoonTools.ECS.System
{
    private Filter TimerFilter;

    public Timing(World world) : base(world)
    {
        TimerFilter = FilterBuilder
            .Include<Timer>()
            .Build();
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var timerEntity in TimerFilter.Entities)
        {
            if (HasOutRelation<Relations.DontTime>(timerEntity))
            {
                continue;
            }

            var timer = Get<Timer>(timerEntity);
            var time = timer.Time - (float)delta.TotalSeconds;

            if (time <= 0)
            {
                if (HasOutRelation<MovementTimer>(timerEntity))
                {
                    var mover = OutRelationSingleton<MovementTimer>(timerEntity);
                    var velocity = Has<IntegerVelocity>(mover) 
                        ? Get<IntegerVelocity>(mover).Value 
                        : Vector2.Zero;

                    // If an enemy and the player moves at the same time to reach a spot, the player wins the tie.
					Send(new DoMovementFirstMessage(mover, velocity));
					//Set(entity, new LastMovedDirection(velocity));

                    // #region walking sfx
                    // if (!HasOutRelation<TimingFootstepAudio>(entity) && framerate > 0)
                    // {
                    // 	PlayRandomFootstep();

                    // 	var footstepTimer = World.CreateEntity();
                    // 	var footstepDuration = Math.Clamp(1f - (framerate / 50f), .5f, 1f);
                    // 	Set(footstepTimer, new Timer(footstepDuration));
                    // 	World.Relate(entity, footstepTimer, new TimingFootstepAudio());
                    // }
                    // #endregion
                }

                /*
                if (HasOutRelation<TeleportToAtTimerEnd>(timerEntity))
                {
                    var outEntity = OutRelationSingleton<TeleportToAtTimerEnd>(timerEntity);
                    var data = World.GetRelationData<TeleportToAtTimerEnd>(timerEntity, outEntity);
                    var entityToTeleportTo = data.TeleportTo;
                    var position = Get<Position>(entityToTeleportTo);
                    Set(outEntity, position);
                }*/

                /*
                if (Has<PlaySoundOnTimerEnd>(entity))
                {
                    var soundMessage = Get<PlaySoundOnTimerEnd>(entity).PlayStaticSoundMessage;
                    Send(soundMessage);
                }*/

                if (!timer.Repeats)
                {
                    Destroy(timerEntity);
                }
                else
                {
                    Set(timerEntity, timer with { Time = timer.Max });
                }
                return;
            }

            Set(timerEntity, timer with { Time = time });
        }
    }

    /*
	private void PlayRandomFootstep()
	{
		Send(
			new PlayStaticSoundMessage(
				new StaticSoundID[]
				{
					StaticAudio.Footstep1,
					StaticAudio.Footstep2,
					StaticAudio.Footstep3,
					StaticAudio.Footstep4,
					StaticAudio.Footstep5,
				}.GetRandomItem<StaticSoundID>(),
			SoundCategory.Generic,
			Rando.Range(0.66f, 0.88f),
			Rando.Range(-.05f, .05f)
			)
		);
	}*/
}