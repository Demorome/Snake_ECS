using System;
using System.Numerics;
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
        .Include<CanDetect>()
        .Include<Direction2D>()
        .Build();
    }

    public override void Update(TimeSpan delta)
    {
        CollisionManipulator.ResetCollidersSpatialHash();

        foreach (var (_, other) in Relations<DetectionVisualPoint>())
        {
            Destroy(other);
        }  

        foreach (var entity in DetecterFilter.Entities)
        {
            var detectionArgs = Get<CanDetect>(entity);
            var angle = MathUtilities.AngleFromUnitVector(Get<Direction2D>(entity).Value);
            var angleStep = float.DegreesToRadians(5f);
            var maxAngle = angle + detectionArgs.ConeRadius;

            // TODO: Delay this removal with a timer?
            foreach (var other in InRelations<Detected>(entity))
            {
                Unrelate<Detected>(other, entity);
            }

            for (float nthAngle = angle - detectionArgs.ConeRadius; nthAngle < maxAngle; nthAngle += angleStep)
            {
                var (hit, stoppedAtEntity) = CollisionManipulator.Raycast_vs_AABBs(entity, nthAngle, detectionArgs.MaxDistance,
                    new Layer(CollisionLayer.None, CollisionLayer.DetectionCone_CollidesWith), // don't need to exclude, since detection cones aren't stored as colliders.
                    CollisionLayer.Player
                );

                Vector2 stopPos;

                if (stoppedAtEntity.HasValue)
                {
                    stopPos = CollisionManipulator.RaycastHits[stoppedAtEntity.Value];
                }
                else
                {
                    var movement = MathUtilities.SafeNormalize(new Vector2(MathF.Cos(nthAngle), MathF.Sin(nthAngle))) * detectionArgs.MaxDistance;
                    stopPos = Get<Position2D>(entity).AsVector() + movement;

                    foreach (var (other, hitPos) in CollisionManipulator.RaycastHits)
                    {
                        if (Has<CanBeDetected>(other))
                        {
                            Relate(other, entity, new Detected());
                        }
                    }
                }

                var pointEntity = CreateEntity();
                Relate(entity, pointEntity, new DetectionVisualPoint());
                Set(pointEntity, new Position2D(stopPos));
            }
        }
    }
}