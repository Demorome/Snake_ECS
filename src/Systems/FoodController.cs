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

public class FoodController : MoonTools.ECS.System
{
	MoonTools.ECS.Filter FoodFilter { get; }

	TileGrid TileGrid;

	public FoodController(World world, TileGrid tileGrid) : base(world)
	{
        TileGrid = tileGrid;

		FoodFilter = 
        FilterBuilder
        .Include<CanBeGrabbed>()
        .Include<GrowsActorOnPickup>()
        .Build();
	}

    public Entity SpawnFood()
    {
        var food = World.CreateEntity();

        World.Set(food, new CanBeGrabbed());
        World.Set(food, new GrowsActorOnPickup());
        World.Set(food, new SpriteAnimation(
            Content.SpriteAnimations.Item_Food, 
            10, 
            true, 
            Utility.Rando.Int(0, Content.SpriteAnimations.Item_Food.Frames.Length))
            );

        if (Utility.Rando.Int(0, 3) == 2)
        {
            // Enemy will burst out from this food!
            World.Set(food, new ColorBlend(Color.Orange));
            World.Set(food, new SpawnsEnemy());

            const float timeForTransformation = 3f;
            {
                var spawnTimer = World.CreateEntity();
                World.Set(spawnTimer, new Timer(timeForTransformation, false));
                World.Relate(spawnTimer, food, new SpawnEnemyFromFood());
            }

            const int numVisualStages = 3;
            {
                var stageTimer = World.CreateEntity();
                World.Set(stageTimer, new Timer(timeForTransformation / numVisualStages, false));
                World.Relate(stageTimer, food, new ChangeStage(0, numVisualStages));
            }
        }

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

        foreach (var message in ReadMessages<Messages.AdvancedEnemySpawningStage>())
        {
            var foodEntity = message.ToChange;
            Color newColor;
            switch(message.NewStage)
            {
            case 1:
                newColor = Color.Crimson;
                break;

            case 2:
                newColor = Color.DarkRed;
                break;

            default:
                newColor = Color.Black;
                break;
            }
            Set(foodEntity, new ColorBlend(newColor));
        }
	}
}
