using System;
using MoonTools.ECS;
using RollAndCash.Components;
using RollAndCash.Relations;
using RollAndCash.Utility;

namespace RollAndCash.Systems;

public class DetectionSystem : MoonTools.ECS.System
{
    Filter DetecterFilter;
    CollisionManipulator CollisionManipulator;

    public DetectionSystem(World world) : base(world)
    {
        CollisionManipulator = new CollisionManipulator(world);

        DetecterFilter = FilterBuilder
        //.Include<CanDetect>()
        .Include<Direction>()
        .Build();
    }

    public override void Update(TimeSpan delta)
    {
        CollisionManipulator.ResetCollidersSpatialHash();

        foreach (var entity in DetecterFilter.Entities)
        {
            //var detectionArgs = Get<CanDetect>(entity);

            var angle = MathUtilities.AngleFromUnitVector(Get<Direction>(entity).Value);
            //var angle = (MathF.PI / 4);
            //var angle = float.DegreesToRadians(45f);

            // TODO: use AngleRadius to determine range of raycasts to shoot.
            //for ()
            //{
            CollisionManipulator.Raycast_vs_AABBs(entity, angle, 100f, CollisionLayer.Level);
            //}
        }
    }
}