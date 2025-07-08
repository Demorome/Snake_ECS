using System;
using System.ComponentModel;
using MoonTools.ECS;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;
using Filter = MoonTools.ECS.Filter;

public class TrailVisualSystem : MoonTools.ECS.System
{
    public Filter TrailFilter;
    VFXManipulator VFXManipulator;

    public TrailVisualSystem(World world) : base(world)
    {
        TrailFilter =
        FilterBuilder
        .Include<HasVisualTrail>()
        .Include<LastPosition>()
        .Build();

        VFXManipulator = new(world);
    }

    public override void Update(System.TimeSpan delta)
    {
        foreach (var entity in TrailFilter.Entities)
        {
            // TODO: Account for HasOutRelation<DontDraw>(entity)

            var lastPos = Get<LastPosition>(entity).Value;
            if (lastPos != Get<Position2D>(entity).AsVector())
            {
                // Spawn trail
                // TODO: Add different trail effects (determined by sub-components)
                var trail = VFXManipulator.SpawnProjectileTrail(entity, Color.White with { A = 200 }, -1f);
            }
        }
    }

}