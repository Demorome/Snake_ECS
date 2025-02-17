using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Input;
using Snake.Relations;
using Snake.Messages;
using Snake.Components;
using System;
using MoonWorks.Graphics;

namespace Snake.Systems;

public class Growth : MoonTools.ECS.System
{
	MoonTools.ECS.Filter PlayerFilter { get; }

	public Growth(World world) : base(world)
	{
		PlayerFilter = FilterBuilder.Include<PlayerIndex>().Build();
	}

    public void SpawnTailPart(Entity player, int amount = 1)
	{
        for (int i = 0; i < amount; ++i)
        {
            var tail = World.CreateEntity();
            var playerIndex = World.Get<PlayerIndex>(player).Value;

            World.Set(tail, playerIndex == 0 ? Color.Green : Color.Blue);
            World.Set(tail, new Depth(World.Get<Depth>(player).Value + 1)); // Draw below player
            //World.Set(tail, new ColorBlend(Color.Cyan));
            World.Set(tail, new SpriteAnimation(Content.SpriteAnimations.NPC_Bizazss_Walk_Down, 0));
            World.Set(tail, new DirectionalSprites(
                Content.SpriteAnimations.NPC_Bizazss_Walk_Up.ID,
                Content.SpriteAnimations.NPC_Bizazss_Walk_Right.ID,
                Content.SpriteAnimations.NPC_Bizazss_Walk_Down.ID,
                Content.SpriteAnimations.NPC_Bizazss_Walk_Left.ID
            ));

            // Connect tail part to player, by finding the lowest part to attach to.
            {
                var lowestPart = player;
                while (HasOutRelation<TailPart>(lowestPart)) 
                {
                    lowestPart = OutRelationSingleton<TailPart>(lowestPart);
                }
                World.Relate(lowestPart, tail, new TailPart());

                // Place the tail on top of the lowest part.
                var lowestPos = World.Get<TilePosition>(lowestPart).PositionVector;
                World.Set(tail, new TilePosition(lowestPos));
                World.Set(tail, new LastTilePosition(lowestPos));
                
                // It will have no collision and won't move until the next time the player moves.
                World.Set(tail, new TailPartBecomeActiveNextMovement());
            }
        }
	}

	public override void Update(TimeSpan timeSpan)
	{
		foreach (var playerEntity in PlayerFilter.Entities)
		{
			//var index = Get<PlayerIndex>(playerEntity).Value;
			foreach (var message in ReadMessages<GrowPlayer>())
			{
				if (message.WhichPlayer == playerEntity)
				{
					SpawnTailPart(playerEntity, message.Amount);
				}
			}
		}
	}
}
