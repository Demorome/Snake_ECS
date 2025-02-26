using System;
using Snake.Components;
using Snake.Content;
using Snake.Data;
using Snake.Messages;
using Snake.Relations;
using Snake.Utility;
using MoonTools.ECS;
using MoonWorks.Graphics;
using MoonWorks.Math;
using System.Numerics;
using MoonWorks.Audio;
using System.Runtime.InteropServices;

namespace Snake.Systems;

public class NPCController : MoonTools.ECS.System
{
	MoonTools.ECS.Filter NPCFilter;
    MoonTools.ECS.Filter TargetFilter;

    TileGrid TileGrid;

	public NPCController(World world, TileGrid tileGrid) : base(world)
	{
        TileGrid = tileGrid;

		NPCFilter =
		FilterBuilder
		.Exclude<PlayerIndex>()
		.Include<TilePosition>()
        .Include<MovementTimer>()
		.Build();

        TargetFilter =
        FilterBuilder
        .Include<TilePosition>()
        .Include<CanBeGrabbed>()
        .Include<GrowsActorOnPickup>()
        .Build();
	}

	public Entity SpawnNPC(Vector2 spawnPosition)
	{
		var npc = World.CreateEntity();

		World.Set(npc, new TilePosition(spawnPosition));
		World.Set(npc, new LastTilePosition(spawnPosition));
		World.Set(npc, new SpriteAnimation(Content.SpriteAnimations.NPC_Drone_Fly_Down, 0));
		World.Set(npc, new Solid());
		World.Set(npc, new Depth(5));
		World.Set(npc, new MovementTimer(0.2f));
		//World.Set(npc, new IntegerVelocity(new Vector2(1, 0))); // Move right
		World.Set(npc, new LastMovedDirection(Vector2.Zero));
		World.Set(npc, new AdjustFramerateToSpeed());

		World.Set(npc, new DirectionalSprites(
			Content.SpriteAnimations.NPC_Drone_Fly_Up.ID,
			Content.SpriteAnimations.NPC_Drone_Fly_Right.ID,
			Content.SpriteAnimations.NPC_Drone_Fly_Down.ID,
			Content.SpriteAnimations.NPC_Drone_Fly_Left.ID
		));

		return npc;
	}

	public override void Update(System.TimeSpan delta)
	{
		if (!Some<GameInProgress>()) { return; }

		var deltaTime = (float)delta.TotalSeconds;

		foreach (var npc in NPCFilter.Entities)
		{
            #region FIND TARGET
            Entity target = default;
            {
                int minDistance = int.MaxValue;
                foreach (var nthTarget in TargetFilter.Entities)
                {
                    var distance = MathUtilities.GetManhattanDistance(
                        Get<TilePosition>(npc).Position, 
                        Get<TilePosition>(nthTarget).Position
                        );

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        target = nthTarget;
                    }
                }
            }
            #endregion

            // doubles as the current Direction
            Vector2 velocity = Has<IntegerVelocity>(npc) ? Get<IntegerVelocity>(npc).Value : Vector2.Zero; 

            if (target != default)
            {
                #region PATHFINDING

                var nextLocation = AStarPathfinding.GetNextLocationToReachTarget(
                    Get<TilePosition>(npc).Position, 
                    Get<TilePosition>(target).Position,
                    TileGrid
                    );

                if (nextLocation != null)
                {
                    velocity = nextLocation.AsVector() - Get<TilePosition>(npc).Position;
                    Set(npc, new IntegerVelocity(velocity));
                }
                
                #endregion
            }

            #region MOVEMENT

            var moveTimer = Get<MovementTimer>(npc);
            var timeLeft = moveTimer.TimeLeftInSecs - deltaTime;
            if (timeLeft <= 0) 
            {
                Send(new DoMovementMessage(npc, velocity));
                //Set(entity, new LastMovedDirection(velocity));

                // Reset movement timer.
                Set(npc, new MovementTimer(moveTimer.Max));
            }
            else {
                // Store the ticking down.
                Set(npc, new MovementTimer(timeLeft, moveTimer.Max));
            }
			#endregion


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
