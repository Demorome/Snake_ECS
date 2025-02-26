using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Input;
using Snake.Relations;
using Snake.Messages;
using Snake.Components;
using System;
using MoonWorks.Graphics;
using System.Numerics;

namespace Snake.Systems;

public class FoodSpawner : MoonTools.ECS.System
{
	MoonTools.ECS.Filter FoodFilter { get; }

	TileGrid TileGrid;

	public FoodSpawner(World world, TileGrid tileGrid) : base(world)
	{
        TileGrid = tileGrid;
		FoodFilter = FilterBuilder.Include<CanBeGrabbed>().Include<GrowsActorOnPickup>().Build();
	}

    public Entity SpawnFood()
    {
        var food = World.CreateEntity();

        World.Set(food, new CanBeGrabbed());
        World.Set(food, new GrowsActorOnPickup());
        World.Set(food, new SpriteAnimation(Content.SpriteAnimations.Item_Food, 10, true, Utility.Rando.Int(0, Content.SpriteAnimations.Item_Food.Frames.Length)));

        // TODO: Handle case where there is no safe spawn position!
        World.Set(food, new TilePosition(TileGrid.GetSafeSpawnPosition()));
        World.Set(food, new Depth(6));
        // World.Set(food, new SlowDownAnimation(15, 1));

        return food;
    }

	public override void Update(TimeSpan timeSpan)
	{
        if (FoodFilter.Empty)
        {
            SpawnFood();
        }
	}
}
