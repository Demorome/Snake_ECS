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

public class TimedChangeManipulator : MoonTools.ECS.Manipulator
{
    public TimedChangeManipulator(World world) : base(world)
    {
    }

    // TODO: Merge FlickeringManipulator here?

    public void SetTimedAlphaChange(
        Entity entity,
        Entity timerEntity,

        byte minStartAlpha = 255, // 0-255
        byte maxStartAlpha = 255,
        byte minEndAlpha = 255,
        byte maxEndAlpha = 255,
        Easing.Function.Float easingMethod = Easing.Function.Float.Linear
        )
    {
        var alpha = (byte)Rando.Int(minStartAlpha, maxStartAlpha);
        Set(entity, new Alpha(alpha));

        var endAlpha = (byte)Rando.Int(minEndAlpha, maxEndAlpha);
        if (endAlpha != alpha)
        {
            Relate(entity, timerEntity, new ChangeAlphaOverTime(alpha, endAlpha, easingMethod));
        }
    }

    public void SetTimedScaleChange(
        Entity entity,
        Entity timerEntity,

        Vector2 minStartScale,
        Vector2 maxStartScale,
        Vector2 minEndScale,
        Vector2 maxEndScale,
        Easing.Function.Float easingMethod = Easing.Function.Float.Linear
        )
    {
        var scale = Rando.Range(minStartScale, maxStartScale);
        Set(entity, new SpriteScale(scale));

        var endScale = Rando.Range(minEndScale, maxEndScale);
        if (endScale != scale)
        {
            Relate(entity, timerEntity, new ChangeSpriteScaleOverTime(scale, endScale, easingMethod));
        }
    }
    
    public void SetTimedRotationChange(
        Entity entity,
        Entity timerEntity,

        float minStartAngle,
        float maxStartAngle,
        float minEndAngle,
        float maxEndAngle,
        Easing.Function.Float easingMethod = Easing.Function.Float.Linear
        )
    {
        var angle = Rando.Range(minStartAngle, maxStartAngle);
        Set(entity, new Angle(angle));

        var endAngle = Rando.Range(minEndAngle, maxEndAngle);
        if (endAngle != angle)
        {
            Relate(entity, timerEntity, new ChangeAngleOverTime(angle, endAngle, easingMethod));
        }
    }
    
}