using System;
using System.ComponentModel;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks.Math;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class ChangeAppearanceOverTime : MoonTools.ECS.System
{
    public ChangeAppearanceOverTime(World world) : base(world)
    {
    }

    // Should be called right after Timing system counts down.
    public override void Update(System.TimeSpan delta)
    {
        foreach (var (entity, timerEntity) in Relations<ChangeSpriteScaleOverTime>())
        {
            var change = GetRelationData<ChangeSpriteScaleOverTime>(entity, timerEntity);
            var timer = Get<Timer>(timerEntity);
            //var time = 1.0f - timer.RemainingPercentage

            var newScale = new Vector2(
                Easing.Interp(change.StartSize.X, change.EndSize.X, timer.Time, timer.Max, change.EasingMethod),
                Easing.Interp(change.StartSize.Y, change.EndSize.Y, timer.Time, timer.Max, change.EasingMethod)
            );
            Set(entity, new SpriteScale(newScale));
        }

        foreach (var (entity, timerEntity) in Relations<ChangeAlphaOverTime>())
        {
            var change = GetRelationData<ChangeAlphaOverTime>(entity, timerEntity);
            var timer = Get<Timer>(timerEntity);
            //var time = 1.0f - timer.RemainingPercentage

            var newAlpha = (byte)Easing.Interp(change.StartAlpha, change.EndAlpha, timer.Time, timer.Max, change.EasingMethod);
            Set(entity, new Alpha(newAlpha));
        }

        foreach (var (entity, timerEntity) in Relations<ChangeAngleOverTime>())
        {
            var change = GetRelationData<ChangeAngleOverTime>(entity, timerEntity);
            var timer = Get<Timer>(timerEntity);
            //var time = 1.0f - timer.RemainingPercentage

            var newAngle = Easing.Interp(change.StartAngle, change.EndAngle, timer.Time, timer.Max, change.EasingMethod);
            Set(entity, new Angle(newAngle));
        }
    }

}