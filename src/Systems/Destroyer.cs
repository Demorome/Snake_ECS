using System;
using MoonTools.ECS;
using RollAndCash.Components;
using RollAndCash.Relations;

namespace RollAndCash.Systems;

// Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
public class Destroyer : MoonTools.ECS.System
{
    public Filter DestroyFilter;

    public Destroyer(World world) : base(world)
    {
        DestroyFilter = 
        FilterBuilder
        .Include<MarkedForDestroy>()
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
    }
}