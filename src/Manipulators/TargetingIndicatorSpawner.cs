using System;
using System.ComponentModel;
using MoonTools.ECS;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class TargetingIndicatorSpawner : MoonTools.ECS.Manipulator
{
    public TargetingIndicatorSpawner(World world) : base(world)
    {
    }

    public Entity CreateTargetingIndicator(
        Entity source, 
        Entity target,
        bool lookTowardsTarget,
        bool stretchTowardsTarget
        )
    {
        var entity = CreateEntity();
        Set(entity, new Position(0, 0));

        // Tie the indicator to the source so it can be deleted if the source is gone.
        Set(entity, new DestroyWhenNoSource());
        Relate(entity, source, new Source());

        // Indicator follows the source
        Relate(entity, source, new PositionFollowing());

        if (lookTowardsTarget || stretchTowardsTarget)
        {
            // Indicator points towards the target
            Relate(entity, target, new VisuallyFollowing(lookTowardsTarget, stretchTowardsTarget));
        }
        return entity;
    }
}