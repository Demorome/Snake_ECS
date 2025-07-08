using System;
using MoonTools.ECS;
using RollAndCash.Relations;
using RollAndCash.Components;
using RollAndCash.Utility;
using System.Numerics;

namespace RollAndCash.Systems;

public class TargetingDirection : MoonTools.ECS.System
{
    private Filter TargetDirectionFilter;

    public TargetingDirection(World world) : base(world)
    {
        TargetDirectionFilter = FilterBuilder
        .Include<Position2D>()
        .Include<Direction2D>()
        .Include<UpdateDirectionToTargetPosition>()
        .Build();
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var entity in TargetDirectionFilter.Entities)
        {
            if (HasOutRelation<DontFollowTarget>(entity))
            {
                continue;
            }

            if (HasOutRelation<Targeting>(entity))
            {
                var targetEntity = OutRelationSingleton<Targeting>(entity);
                var targetPos = Get<Position2D>(targetEntity).AsVector();
                Set(entity, new TargetPosition(targetPos));
            }
            
            if (Has<TargetPosition>(entity))
            {
                var updateData = Get<UpdateDirectionToTargetPosition>(entity);
                var targetPosition = Get<TargetPosition>(entity).Value;
                var projectilePosition = Get<Position2D>(entity).AsVector();
                Vector2 projectileDirection = MathUtilities.SafeNormalize(targetPosition - projectilePosition);
                //Vector2 projectileDirection = new Vector2(MathF.Sin(angleToPlayer), MathF.Cos(angleToPlayer));
                Set(entity, new Direction2D(projectileDirection));

                if (updateData.DoOnce)
                {
                    Remove<UpdateDirectionToTargetPosition>(entity);
                }
            }
            else
            {
                Remove<UpdateDirectionToTargetPosition>(entity);
            }
        }
    }
}
