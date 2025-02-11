using System;
using System.Numerics;
using MoonTools.ECS;
using Snake.Utility;
using Snake.Components;
using Snake.Relations;
using Snake.Messages;
using Snake.Content;

namespace Snake.Systems;

public class Motion : MoonTools.ECS.System
{
    Filter VelocityFilter;
    //Filter SolidFilter;

    Entity[,] Grid = new Entity[GridInfo.WidthWithWalls, GridInfo.HeightWithWalls]; // [Row, Column]

    public Motion(World world) : base(world)
    {
        // Fill the Grid
        for (int i = 0; i < GridInfo.WidthWithWalls; i++)
        {
            for (int j = 0; j < GridInfo.HeightWithWalls; j++)
            {
                if (j == 0 || i == 0 || (i == (GridInfo.WidthWithWalls-1)) || (j == (GridInfo.HeightWithWalls-1)))
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
        //SolidFilter = FilterBuilder.Include<TilePosition>()/*.Include<Rectangle>()*/.Include<Solid>().Build();
    }

    void UpdateTilePosition(Entity e, Vector2 newPos)
    {
        Set(e, new TilePosition(newPos));
        Set(e, new PixelPosition(GridInfo.TilePositionToPixelPosition(newPos)));
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
                            UpdateTilePosition(tailPart, upperPos);

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
                    UpdateTilePosition(entity, nextPos);

                    // Reset velocity (lost after moving)
                    Set(entity, new IntegerVelocity(Vector2.Zero));
                }
            }
        }
    }
}
