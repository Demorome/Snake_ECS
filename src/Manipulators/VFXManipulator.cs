using System;
using System.ComponentModel;
using System.Dynamic;
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

// Used to create VFX entities
// Also applies preset visual changes like rotating over a period of time (not only for VFX entities).
public class VFXManipulator : MoonTools.ECS.Manipulator
{
    TimedChangeManipulator TimedChangeManipulator;

    public VFXManipulator(World world) : base(world)
    {
        TimedChangeManipulator = new TimedChangeManipulator(world);
    }

    public Entity SpawnProjectileTrail(
        Entity projectile,
        Color color,
        float depth
        )
    {
        var lastPos = new Position2D(Get<LastPosition>(projectile).Value);
        var projectilePos = Get<Position2D>(projectile).AsVector();
        var trailSprite = new SpriteAnimation(SpriteAnimations.ProjectileTrail);

        Entity trail = CreateVFX(lastPos, trailSprite, depth, 0.5f, 0.5f);
        Set(trail, new ColorBlend(color));

        Set(trail, new Angle(MathUtilities.GetHeadingAngle(lastPos.AsVector(), projectilePos)));

        // TODO: Center scale changes based on sprite origin!!!!!

        // Stretch from projectile lastPos -> currentPos
        var distance = Vector2.Distance(projectilePos, lastPos.AsVector());
        var startScale = new Vector2(distance, 1f); // TODO: Account for size of sprites to reduce distance!

        // Shrinks down to form a thin line at the end of the trail.
        var endScale = new Vector2(startScale.X, 0.0f); // only shrink the width of the trail
        TimedChangeManipulator.SetTimedScaleChange(trail, trail,
            endScale, endScale, // order of args backwards on purpose
            startScale, startScale,
            Easing.Function.Float.OutCubic
        );

        Relate(projectile, trail, new TrailingVisuals());

        return trail;
    }

    // TODO: add option for Additive vs Normal blending
    // TODO: "wiggle" value for each property with a min/max value
    // TODO: Friction?
    // Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
    // I decided to break up the min/max start/end values into their own functions.
    public Entity CreateVFX(
        Position2D position,
        SpriteAnimation sprite,
        float depth,

        float minTimeToLive = -1f,
        float maxTimeToLive = -1f,  // ignored if <= 0

        float minSpeed = 0.0f,
        float maxSpeed = 0.0f,
        float? speedAcceleration = null,
        // TODO: Speed easing method
        Vector2? direction = null
        )
    {
        var vfx = CreateEntity();

        Set(vfx, position);
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
            Set(vfx, new Direction2D(direction.Value));
        }
        else if (speed != 0.0f)
        {
            throw new Exception("Set a direction if you want to have a speed.");
        }

        if (maxTimeToLive > 0.0f)
        {
            var lifeTime = Rando.Range(minTimeToLive, maxTimeToLive);
            Set(vfx, new Timer(lifeTime));
        }

        return vfx;
    }

    public void MakeVFXFollowSource(
        Entity vfx,
        Entity source,
        bool lagBehind = false,
        float alphaMult = 1.0f,
        bool copySourceSpriteFrames = false,
        bool hideIfSourceSpeedIsZero = false
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

    #region Prefabs
    public Entity CreateTargetingVisual(Entity source, Entity target)
    {
        var sprite = new SpriteAnimation(SpriteAnimations.Pixel);

        Entity targetingVisual = CreateVFX(
            Get<Position2D>(source),
            sprite,
            -1f
        );

        MakeVFXFollowSource(targetingVisual, source);
        MakeVFXPointAtTarget(targetingVisual, target, true, true);

        return targetingVisual;
    }
    
    #endregion Prefabs
}