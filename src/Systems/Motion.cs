using System;
using System.Numerics;
using MoonTools.ECS;
using Snake.Utility;
using Snake.Components;
using Snake.Relations;
using Snake.Messages;
using Snake.Content;
using MoonWorks.Storage;

namespace Snake.Systems;

public class Motion : MoonTools.ECS.System
{
    Filter VelocityFilter;
    //Filter SolidFilter;

    TileGrid TileGrid;

    public Motion(World world, TileGrid tileGrid) : base(world)
    {
        TileGrid = tileGrid;

        // Fill the Grid
        /*
        for (int i = 0; i < GridInfo.WidthWithWalls; i++)
        {
            for (int j = 0; j < GridInfo.HeightWithWalls; j++)
            {
                Grid[i, j] = default;
            }
        }*/

        // TODO: Fruit / interact filter
        VelocityFilter = FilterBuilder.Include<TilePosition>().Include<IntegerVelocity>().Build();
        //SolidFilter = FilterBuilder.Include<TilePosition>()/*.Include<Rectangle>()*/.Include<Solid>().Build();
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

        var entityAtNextPos = TileGrid.Grid[(int)nextPos.X, (int)nextPos.Y];

        // If there's no collision...
        if (entityAtNextPos == default || !Has<Solid>(entityAtNextPos))
        {
            return (entityAtNextPos, false);
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

                var result = CheckSolidCollision(entity);
                if (result.hit) {
                    //Relate(entity, result.other, new TouchingSolid());
                    Send(new EndGame());
                    Set(entity, new LastTilePosition(oldPos));
                }
                else
                {
                    if (Has<CanBeGrabbed>(result.other)) 
                    {
                        if (Has<GrowsPlayerOnPickup>(result.other))
                        {
                            Send(new GrowPlayer(entity));
                        }
                        Destroy(result.other);
                        // Don't need to empty out the space in the Grid, since it will be filled up with the player soon.
                    }

                    // Update position
                    TileGrid.UpdateTilePosition(entity, nextPos);

                    // Reset velocity (lost after moving)
                    Set(entity, new IntegerVelocity(Vector2.Zero));

                    // Update movement for tail parts
                    if (HasOutRelation<TailPart>(entity))
                    {
                        var upperPart = entity;
                        Entity tailPart = OutRelationSingleton<TailPart>(upperPart);
                        // Go through the linked list, starting from the head.
                        while (true)
                        {
                            var lowerPos = Get<TilePosition>(tailPart).PositionVector;
                            var upperPos = Get<LastTilePosition>(upperPart).Position;
#if DEBUG
                            //Console.WriteLine("Tail TilePosition: {0}", lowerPos);
#endif

                            Set(tailPart, new LastMovedDirection(Get<LastMovedDirection>(upperPart).Direction));

                            if (Has<TailPartBecomeActiveNextMovement>(tailPart))
                            {
                                // Don't move this yet (lowerPos == upperPos, since it's spawned on top of parent).
                                Set(tailPart, new Solid());
                                Set(tailPart, new PixelPosition(GridInfo.TilePositionToPixelPosition(lowerPos)));
                                Remove<TailPartBecomeActiveNextMovement>(tailPart);
                                // Don't bother checking lower tail parts; they probably need to wait a couple turns before becoming active.
                                break;
                            }
                            else 
                            {
                                TileGrid.UpdateTilePosition(tailPart, upperPos);
                            }

                            // Checks for next iteration
                            if (!HasOutRelation<TailPart>(tailPart)) {
                                TileGrid.Grid[(int)lowerPos.X, (int)lowerPos.Y] = default; // leave a blank space behind
                                break;
                            }
                            
                            upperPart = tailPart;
                            tailPart = OutRelationSingleton<TailPart>(upperPart);
                        }
                    }
                    else {
                        TileGrid.Grid[(int)oldPos.X, (int)oldPos.Y] = default; // leave a blank space behind
                    }
                }
            }
        }
    }
}
