using System;
using MoonTools.ECS;
using RollAndCash.Components;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;
using Timed = RollAndCash.Components.Timed;

namespace RollAndCash.Systems;

public class FlipAnimationSystem : MoonTools.ECS.System
{

    public FlipAnimationSystem(World world) : base(world)
    {
    }

    public override void Update(TimeSpan delta)
    {
        var deltaTime = (float)delta.TotalSeconds;

        //foreach (var message in )

        foreach (var (entity, flipTimerEntity) in Relations<Relations.WillFlipHorizontally>())
        {
            var flipTimer = Get<Timed>(flipTimerEntity);
            var flipData = GetRelationData<Relations.WillFlipHorizontally>(entity, flipTimerEntity);

            if (TimeUtilities.OnTime(
                flipTimer.Time, 
                flipData.TimePerFlip, 
                flipData.TimePerFlip + deltaTime, 
                flipData.TimePerFlip * 2)
                )
            {
                Relate(entity, flipTimerEntity, new FlippedHorizontallyByTarget());
            }
            else
            {
                Unrelate<FlippedHorizontallyByTarget>(entity, flipTimerEntity);
            }
        }

        foreach (var (entity, flipTimerEntity) in Relations<Relations.WillFlipVertically>())
        {
            var flipTimer = Get<Timed>(flipTimerEntity);
            var flipData = GetRelationData<Relations.WillFlipVertically>(entity, flipTimerEntity);

            if (TimeUtilities.OnTime(
                flipTimer.Time, 
                flipData.TimePerFlip, 
                flipData.TimePerFlip + deltaTime, 
                flipData.TimePerFlip * 2)
                )
            {
                Relate(entity, flipTimerEntity, new FlippedVerticallyByTarget());
            }
            else
            {
                Unrelate<FlippedVerticallyByTarget>(entity, flipTimerEntity);
            }
        }

        foreach (var (entity, rotationTimerEntity) in Relations<Relations.WillRotate>())
        {
            var rotateTimer = Get<Timed>(rotationTimerEntity);
            var rotateData = GetRelationData<Relations.WillRotate>(entity, rotationTimerEntity);

            if (TimeUtilities.OnTime(
                rotateTimer.Time, 
                rotateData.TimePerRotation, 
                rotateData.TimePerRotation + deltaTime, 
                rotateData.TimePerRotation * 2)
                )
            {
                Relate(entity, rotationTimerEntity, new Rotated(rotateData.Angle));
            }
            else
            {
                Unrelate<Rotated>(entity, rotationTimerEntity);
            }
        }
    }
}