using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Input;
using Snake.Relations;
using Snake.Messages;
using Snake.Components;
using System;
using MoonWorks.Graphics;
using Snake.Utility;

namespace Snake.Systems;

public class Growth : MoonTools.ECS.System
{
	MoonTools.ECS.Filter GrowingActorFilter { get; }

	public Growth(World world) : base(world)
	{
		GrowingActorFilter = 
        FilterBuilder
        .Include<TilePosition>()
        .Include<CanGrow>()
        .Build();
	}

    public void SpawnTailPart(Entity actor, int amount = 1)
	{
        for (int i = 0; i < amount; ++i)
        {
            var tail = World.CreateEntity();

            //var randomColor = new Color(Rando.Range(0, 1), Rando.Range(0, 1), Rando.Range(0, 1));
            Color color = Has<PlayerIndex>(actor) ? Color.Green : Color.Red;
            World.Set(tail, new ColorBlend(color));
            World.Set(tail, new Depth(World.Get<Depth>(actor).Value + 1)); // Draw below actor
            World.Set(tail, new SpriteAnimation(Content.SpriteAnimations.NPC_Bizazss_Walk_Down, 0));
            World.Set(tail, new DirectionalSprites(
                Content.SpriteAnimations.NPC_Bizazss_Walk_Up.ID,
                Content.SpriteAnimations.NPC_Bizazss_Walk_Right.ID,
                Content.SpriteAnimations.NPC_Bizazss_Walk_Down.ID,
                Content.SpriteAnimations.NPC_Bizazss_Walk_Left.ID
            ));
            World.Set(tail, new AdjustFramerateToTopParentSpeed());
            World.Set(tail, new TopParent(actor));

            // Connect tail part to actor, by finding the lowest part to attach to.
            {
                var lowestPart = actor;
                while (HasOutRelation<TailPart>(lowestPart)) 
                {
                    lowestPart = OutRelationSingleton<TailPart>(lowestPart);
                }
                World.Relate(lowestPart, tail, new TailPart());

                // Place the tail on top of the lowest part.
                var lowestPos = World.Get<TilePosition>(lowestPart).Position;
                World.Set(tail, new TilePosition(lowestPos));
                World.Set(tail, new LastTilePosition(lowestPos));
                
                // It will have no collision and won't move until the next time the player moves.
                World.Set(tail, new TailPartBecomeActiveNextMovement());
            }
        }
	}

	public override void Update(TimeSpan timeSpan)
	{
		foreach (var actor in GrowingActorFilter.Entities)
		{
			foreach (var message in ReadMessages<GrowActor>())
			{
				if (message.WhichActor == actor)
				{
					SpawnTailPart(actor, message.Amount);
				}
			}
		}
	}
}
