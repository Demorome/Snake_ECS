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
        .Include<CanMove>()
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
        World.Set(npc, new CanMove());

        {
            var moveTimer = World.CreateEntity();
            World.Set(moveTimer, new Timer(0.2f, true));
            World.Relate(moveTimer, npc, new MovementTimer());
            //World.Set(npc, new IntegerVelocity(new Vector2(1, 0))); // Move right
        }

		World.Set(npc, new LastMovedDirection(Vector2.Zero));
		World.Set(npc, new AdjustFramerateToSpeed());
        World.Set(npc, new CanGrow());

		World.Set(npc, new DirectionalSprites(
			Content.SpriteAnimations.NPC_Drone_Fly_Up.ID,
			Content.SpriteAnimations.NPC_Drone_Fly_Right.ID,
			Content.SpriteAnimations.NPC_Drone_Fly_Down.ID,
			Content.SpriteAnimations.NPC_Drone_Fly_Left.ID
		));

		return npc;
	}

    Entity FindTarget(Entity npc)
    {
        Entity target = default;
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
        return target;
    }

	public override void Update(System.TimeSpan delta)
	{
		if (!Some<GameInProgress>()) { return; }

		var deltaTime = (float)delta.TotalSeconds;

        #region SPAWN NPCs
        foreach (var message in ReadMessages<SpawnEnemy>())
        {
            if (TileGrid.IsTileEmpty((int)message.Position.X, (int)message.Position.Y))
            {
                var npc = SpawnNPC(message.Position);
                if (message.NumTailParts > 0)
                {
                    Send(new GrowActor(npc, message.NumTailParts));
                }
            }
        }

        /*
        if (NPCFilter.Empty)
        {
            SpawnNPC(TileGrid.GetSafeSpawnPosition());
        }*/
        #endregion

		foreach (var npc in NPCFilter.Entities)
		{
            var npcPosition = Get<TilePosition>(npc).Position;

            // doubles as the current Direction
            Vector2 velocity = Has<IntegerVelocity>(npc) ? Get<IntegerVelocity>(npc).Value : Vector2.Zero; 

            var target = FindTarget(npc);
            if (target != default)
            {
                #region PATHFINDING

                var nextLocation = AStarPathfinding.GetNextLocationToReachTarget(
                    npcPosition,
                    Get<TilePosition>(target).Position,
                    (x, y) => !TileGrid.IsSpaceOccupiedBySolid(x, y) || TileGrid.IsSpaceOccupiedByPlayer(x, y)
                    );

                if (nextLocation != null)
                {
                    velocity = nextLocation.AsVector() - npcPosition;
                    Set(npc, new IntegerVelocity(velocity));
                }

                #endregion
            }
		}
	}
}
