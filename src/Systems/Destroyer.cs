using System;
using MoonTools.ECS;
using RollAndCash.Components;
using RollAndCash.Relations;

namespace RollAndCash.Systems;

// Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
public class Destroyer : MoonTools.ECS.System
{
    Filter DestroyFilter;
    Filter DestroyWhenNoTargetingSourceFilter;

    public Destroyer(World world) : base(world)
    {
        DestroyFilter = FilterBuilder
        .Include<MarkedForDestroy>()
        .Build();

        DestroyWhenNoTargetingSourceFilter = FilterBuilder
        .Include<DestroyWhenNoSource>()
        .Build();
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var entity in DestroyFilter.Entities)
        {
            /*
            foreach (var collider in OutRelations<HasCollider>(entity))
                Destroy(collider);
            */

            Destroy(entity);
        }

        foreach (var entity in DestroyWhenNoTargetingSourceFilter.Entities)
        {
            if (Has<DestroyWhenNoSource>(entity) && !HasOutRelation<Source>(entity))
            {
                Destroy(entity);
            }
        }
    }
}