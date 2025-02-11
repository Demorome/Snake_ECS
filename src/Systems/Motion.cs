using System;
using System.Numerics;
using MoonTools.ECS;
using RollAndCash.Utility;
using RollAndCash.Components;
using RollAndCash.Relations;
using RollAndCash.Messages;
using RollAndCash.Content;

namespace RollAndCash.Systems;

public class Motion : MoonTools.ECS.System
{
    Filter VelocityFilter;
    Filter SolidFilter;

    //SpatialHash<Entity> SolidSpatialHash = new SpatialHash<Entity>(0, 0, Dimensions.GAME_W, Dimensions.GAME_H, 32);
    const int GridWidth = 10;
    const int GridHeight = 10;
    // Adding 2 to each dimension to account for outer wall layer, which will have Wall entities.
    const int GridWidthWithWalls = GridWidth + 2;
    const int GridHeightWithWalls = GridHeight + 2;
    Entity[,] Grid = new Entity[GridWidthWithWalls,GridHeightWithWalls]; // [Row, Column]

    public Motion(World world) : base(world)
    {
        // Fill the Grid
        for (int i = 0; i < GridWidthWithWalls; i++)
        {
            for (int j = 0; j < GridHeightWithWalls; j++)
            {
                if (j == 0 || i == 0 || (i == (GridWidthWithWalls-1)) || (j == (GridHeightWithWalls-1)))
                {
                    //Grid[i, j] = Wall;
                }
                else 
                {
                    Grid[i, j] = default;
                }
            }
        }

        // TODO: Fruit / interact filter
        VelocityFilter = FilterBuilder.Include<TilePosition>().Include<IntegerVelocity>().Build();
        SolidFilter = FilterBuilder.Include<TilePosition>()/*.Include<Rectangle>()*/.Include<Solid>().Build();
    }

    // Check if the spot our entity wants to move to is already occupied by a Solid.
    (Entity other, bool hit) CheckSolidCollision(Entity e)
    {
        if (!Has<Solid>(e)) 
        {
            return (default, false);
        }

        var oldPos = Get<TilePosition>(e).PositionVector;
        var nextPos = oldPos + Get<IntegerVelocity>(e).Value;

        var entityAtNextPos = Grid[(int)nextPos.X, (int)nextPos.Y];

        // If there's no collision...
        if (entityAtNextPos == default || !Has<Solid>(entityAtNextPos))
        {
            return (default, false);
        }
        else {
            return (entityAtNextPos, true);
        }
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var entity in VelocityFilter.Entities)
        {
            /*
            if (HasOutRelation<DontMove>(entity))
                continue;*/

            var vel = Get<IntegerVelocity>(entity).Value;

            if (vel != Vector2.Zero) {
                var oldPos = Get<TilePosition>(entity).PositionVector;
                var nextPos = oldPos + vel;

                // TODO: Check if we collided with a fruit here?

                var result = CheckSolidCollision(entity);
                if (result.hit) {
                    Relate(entity, result.other, new TouchingSolid());
                }
                else {
                    // Update movement for tail parts
                    if (HasInRelation<TailPart>(entity))
                    {
                        var upperPart = entity;
                        Entity tailPart = InRelationSingleton<TailPart>(upperPart);
                        // Go through the linked list, starting from the head.
                        while (true)
                        {
                            var lowerPos = Get<TilePosition>(tailPart).PositionVector;
                            var upperPos = Get<TilePosition>(upperPart).PositionVector;

                            //Grid[(int)lowerPos.X, (int)lowerPos.Y] = default; // leave a blank space behind     **only needed for debugging
                            Grid[(int)upperPos.X, (int)upperPos.Y] = tailPart; // moving on
                            Set(tailPart, new TilePosition(upperPos));

                            // Checks for next iteration
                            if (!HasInRelation<TailPart>(tailPart)) {
                                Grid[(int)lowerPos.X, (int)lowerPos.Y] = default; // leave a blank space behind 
                                break;
                            }
                            
                            upperPart = tailPart;
                            tailPart = InRelationSingleton<TailPart>(upperPart);
                        }
                    }
                    else {
                        Grid[(int)oldPos.X, (int)oldPos.Y] = default; // leave a blank space behind
                    }

                    // Update position
                    Grid[(int)nextPos.X, (int)nextPos.Y] = entity; // moving on
                    Set(entity, new TilePosition(nextPos));

                    // Reset velocity (lost after moving)
                    Set(entity, new IntegerVelocity(Vector2.Zero));
                }
            }
        }
    }
}
