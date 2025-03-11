using System;
using System.ComponentModel;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks.Graphics;
using MoonWorks.Math;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Data;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class FXSpawner : MoonTools.ECS.Manipulator
{
    public FXSpawner(World world) : base(world)
    {
    }

    // TODO: Add prefab effect functions
    // TODO: add option for Additive vs Normal blending
    // TODO: "wiggle" value for each property with a min/max value
    // TODO: Friction?
    // Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
    public Entity CreateVFX(
        Vector2 minPos,
        Vector2 maxPos,
        SpriteAnimation sprite,
        float depth,
        
        float minTimeToLive = -1f,
        float maxTimeToLive = -1f,  // ignored if <= 0

        float minSpeed = 0.0f,
        float maxSpeed = 0.0f,
        float? speedAcceleration = null,
        // TODO: Speed easing method
        Vector2? direction = null,

        Vector2? minStartSize = null,
        Vector2? maxStartSize = null,
        Vector2? minEndSize = null,
        Vector2? maxEndSize = null,
        Easing.Function.Float sizeEasingMethod = Easing.Function.Float.Linear,

        byte minStartAlpha = 255, // 0-255
        byte maxStartAlpha = 255,
        byte minEndAlpha = 255,
        byte maxEndAlpha = 255,
        Easing.Function.Float alphaEasingMethod = Easing.Function.Float.Linear,

        float minStartAngle = 0.0f,
        float maxStartAngle = 0.0f,
        float minEndAngle = 0.0f,
        float maxEndAngle = 0.0f,
        Easing.Function.Float angleEasingMethod = Easing.Function.Float.Linear
        )
    {
        var vfx = CreateEntity();

        Vector2 position = new Vector2(Rando.Range(minPos.X, maxPos.X), Rando.Range(minPos.Y, maxPos.Y));
        Set(vfx, new Position(position));
        Set(vfx, sprite);
        Set(vfx, new Depth(depth));

        var speed = Rando.Range(minSpeed, maxSpeed);
        if (speed != 0.0f)
        {
            Set(vfx, new Speed(speed));
            if (speedAcceleration != null)
            {
                Set(vfx, new SpeedAcceleration(speedAcceleration.Value));
            }
        }

        if (direction != null)
        {
            Set(vfx, new Direction(direction.Value));
        }
        else if (speed != 0.0f)
        {
            throw new Exception("Set a direction if you want to have a speed.");
        }

        // could filter out if the result is 0.0f, but whatever
        var angle = Rando.Range(minStartAngle, maxStartAngle);
        Set(vfx, new Angle(angle));

        var alpha = (byte)Rando.Int(minStartAlpha, maxStartAlpha);
        Set(vfx, new Alpha(alpha));

        var size = Vector2.One;
        if (minStartSize != null)
        {
            // assume maxStartSize won't be null either
            size = Rando.Range(minStartSize.Value, maxStartSize.Value);
            Set(vfx, new SpriteScale(size));
        }

        if (maxTimeToLive > 0.0f)
        {
            var timer = CreateEntity();
            var lifeTime = Rando.Range(minTimeToLive, maxTimeToLive);
            Set(timer, new Timer(lifeTime));
            Relate(timer, vfx, new DeleteWhenTimerEnds());

            if (minEndSize != null)
            {
                var endSize = Rando.Range(minEndSize.Value, maxEndSize.Value);
                if (endSize != size)
                {
                    Relate(vfx, timer, new ChangeSpriteScaleOverTime(size, endSize, sizeEasingMethod));
                }
            }

            {
                var endAlpha = (byte)Rando.Int(minEndAlpha, maxEndAlpha);
                if (endAlpha != alpha)
                {
                    Relate(vfx, timer, new ChangeAlphaOverTime(alpha, endAlpha, alphaEasingMethod));
                }
            }

            {
                var endAngle = Rando.Range(minEndAngle, maxEndAngle);
                if (endAngle != angle)
                {
                    Relate(vfx, timer, new ChangeAngleOverTime(angle, endAngle, angleEasingMethod));
                }
            }
        }

        return vfx;
    }

    public void MakeVFXFollowSource(
        Entity vfx, 
        Entity source,
        bool lagBehind = false,
        float alphaMult = 1.0f
        )
    {
        if (alphaMult != 1.0f)
        {
            var color = Has<ColorBlend>(source) ? Get<ColorBlend>(source).Color : Color.White;
            color.A = (byte)(color.A * alphaMult);
            Set(vfx, new ColorBlend(color));
        }

        // TODO: Add offset, or use last_position
        //Relate(source, vfx, new TrailingVisuals());
        Relate(vfx, source, new PositionFollowing(/*lagBehind*/));
        Relate(vfx, source, new Source());
        Set(vfx, new DestroyWhenNoSource());
    }

    public void MakeVFXPointAtTarget(
        Entity vfx, 
        Entity target,
        bool lookTowardsTarget,
        bool stretchTowardsTarget,
        bool destroyIfTargetGone = true
        )
    {
        var color = Has<ColorBlend>(target) ? Get<ColorBlend>(target).Color : Color.White;
        color.A /= 2; // transparency

        Set(vfx, new ColorBlend(color));

        Relate(vfx, target, new PositionFollowing()); // TODO: Add offset, or use last_position
        Relate(vfx, target, new Targeting());
        if (destroyIfTargetGone)
        {
            Set(vfx, new DestroyWhenNoTarget());
        }

        if (lookTowardsTarget || stretchTowardsTarget)
        {
            // Indicator points towards the target
            Relate(vfx, target, new VisuallyFollowing(lookTowardsTarget, stretchTowardsTarget));
        }
    }
}