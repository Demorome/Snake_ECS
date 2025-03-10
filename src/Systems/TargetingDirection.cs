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
        .Include<Position>()
        .Include<Direction>()
        .Include<UpdateDirectionToTargetPosition>()
        .Build();
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var entity in TargetDirectionFilter.Entities)
        {
            if (HasOutRelation<DontUpdateDirection>(entity))
            {
                continue;
            }

            if (HasOutRelation<TargetingEntity>(entity))
            {
                var targetEntity = OutRelationSingleton<TargetingEntity>(entity);
                var targetPos = Get<Position>(targetEntity).AsVector();
                Set(entity, new TargetPosition(targetPos));
            }
            
            if (Has<TargetPosition>(entity))
            {
                var updateData = Get<UpdateDirectionToTargetPosition>(entity);
                var targetPosition = Get<TargetPosition>(entity).Value;
                var projectilePosition = Get<Position>(entity).AsVector();
                Vector2 projectileDirection = MathUtilities.SafeNormalize(targetPosition - projectilePosition);
                //Vector2 projectileDirection = new Vector2(MathF.Sin(angleToPlayer), MathF.Cos(angleToPlayer));
                Set(entity, new Direction(projectileDirection));

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
