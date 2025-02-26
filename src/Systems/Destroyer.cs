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
public class Destroyer : MoonTools.ECS.System
{
    public MoonTools.ECS.Filter DestroyFilter;
    TileGrid TileGrid;

    public Destroyer(World world, TileGrid tileGrid) : base(world)
    {
        TileGrid = tileGrid;

        DestroyFilter = FilterBuilder
                        .Include<MarkedForDestroy>()
                        .Build();
    }

    void DestroyAndReclaimTileSpace(Entity entity)
    {
        if (Has<TilePosition>(entity))
        {
            var position = Get<TilePosition>(entity);
            TileGrid.EmptyOutTile(position.X, position.Y);
        }
        Destroy(entity);
    }

    void DestroyTailParts(Entity entity)
    {
        // Get lowest part
        var lowestPart = entity;
        while (HasOutRelation<TailPart>(lowestPart)) 
        {
            lowestPart = OutRelationSingleton<TailPart>(lowestPart);
        }

        var nextPart = lowestPart;
        Entity currentPart;
        // Destroy every tail part as we work our way back up
        while (HasInRelation<TailPart>(nextPart)) 
        {
            currentPart = nextPart;
            nextPart = InRelationSingleton<TailPart>(nextPart);
            DestroyAndReclaimTileSpace(currentPart);
        }
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var entity in DestroyFilter.Entities)
        {
            DestroyTailParts(entity);

            if (Has<PlayerIndex>(entity))
            {
                Send(new EndGame());
            }

            DestroyAndReclaimTileSpace(entity);
        }
    }
}