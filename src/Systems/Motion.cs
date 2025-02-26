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
    //Filter VelocityFilter;
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
        //VelocityFilter = FilterBuilder.Include<TilePosition>().Include<IntegerVelocity>().Build();
        //SolidFilter = FilterBuilder.Include<TilePosition>()/*.Include<Rectangle>()*/.Include<Solid>().Build();
    }
    
    // Check if the spot our entity wants to move to is already occupied by a Solid.
    (Entity other, bool hit) CheckSolidCollision(Entity e)
    {
        if (!Has<Solid>(e)) 
        {
            return (default, false);
        }

        var oldPos = Get<TilePosition>(e).Position;
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

    void MoveTailParts(Entity parent)
    {
        var upperPart = parent;
        Entity tailPart = OutRelationSingleton<TailPart>(upperPart);
        // Go through the linked list, starting from the head.
        while (true)
        {
            var lowerPos = Get<TilePosition>(tailPart).Position;
            var upperPos = Get<LastTilePosition>(upperPart).Position;
#if DEBUG
            //Console.WriteLine("Tail TilePosition: {0}", lowerPos);
#endif

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
                // leave a blank space behind
                TileGrid.EmptyOutTile((int)lowerPos.X, (int)lowerPos.Y); 
                break;
            }
            
            upperPart = tailPart;
            tailPart = OutRelationSingleton<TailPart>(upperPart);
        }
    }

    void HandleMovement(Entity entity, Vector2 velocity)
    {
        /*
        if (HasOutRelation<DontMove>(entity))
            continue;*/

        //var vel = Get<IntegerVelocity>(entity).Value;

        if (velocity != Vector2.Zero) {
            var oldPos = Get<TilePosition>(entity).Position;
            var nextPos = oldPos + velocity;

            var result = CheckSolidCollision(entity);
            if (result.hit) {
                Set(entity, new MarkedForDestroy());
                Set(entity, new LastTilePosition(oldPos));
            }
            else
            {
                if (Has<CanBeGrabbed>(result.other)) 
                {
                    if (Has<GrowsActorOnPickup>(result.other))
                    {
                        Send(new GrowActor(entity));
                    }
                    Destroy(result.other);
                    // Don't need to empty out the space in the Grid, since it will be filled up with the player soon.
                }

                // Update position
                TileGrid.UpdateTilePosition(entity, nextPos);
                if (HasOutRelation<TailPart>(entity))
                {
                    MoveTailParts(entity);
                }
                else {
                    // leave a blank space behind
                    TileGrid.EmptyOutTile((int)oldPos.X, (int)oldPos.Y); 
                }
            }
        }
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var message in ReadMessages<DoMovementFirstMessage>())
        {
            HandleMovement(message.Entity, message.Velocity);
        }

        foreach (var message in ReadMessages<DoMovementMessage>())
        {
            HandleMovement(message.Entity, message.Velocity);
        }
    }
}
