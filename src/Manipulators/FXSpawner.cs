using System;
using System.ComponentModel;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks.Graphics;
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
    // Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
    public Entity CreateVFX(
        Vector2 minPos,
        Vector2 maxPos,
        SpriteAnimation sprite,
        float depth,
        float minTimeToLive = -1f,
        float maxTimeToLive = -1f,  // ignored if <= 0
        float minSpeed = 1f,
        float maxSpeed = 1f,
        float? acceleration = null,
        Vector2? direction = null,
        float minAngle = 0.0f,
        float maxAngle = 0.0f,
        Vector2? sizeChangeSpeed = null,
        float minFadeSpeed = 0.0f,
        float maxFadeSpeed = 0.0f,
        float minRotationSpeed = 0.0f,
        float maxRotationSpeed = 0.0f
        // TODO: Fade mult or some lerp shit
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
            if (acceleration != null)
            {
                Set(vfx, new Acceleration(acceleration.Value));
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

        // could filter on in the result is 0.0f, but whatever
        Set(vfx, new Angle(Rando.Range(minAngle, maxAngle)));

        if (maxTimeToLive > 0.0f)
        {
            var timer = CreateEntity();
            var lifeTime = Rando.Range(minTimeToLive, maxTimeToLive);
            Set(timer, new Timer(lifeTime));
            Relate(timer, vfx, new DeleteWhenTimerEnds());

            if (sizeChangeSpeed != null)
            {
                Relate(vfx, timer, new ChangeSizeOverTime(sizeChangeSpeed.Value));
            }

            var fadeSpeed = Rando.Range(maxFadeSpeed, minFadeSpeed);
            if (fadeSpeed > 0.0f)
            {
                Relate(vfx, timer, new FadeOverTime(fadeSpeed));
            }

            var rotationSpeed = Rando.Range(maxRotationSpeed, minRotationSpeed);
            if (rotationSpeed > 0.0f)
            {
                Relate(vfx, timer, new RotateOverTime(rotationSpeed));
            }
        }

        return vfx;
    }

    public void MakeVFXTrailBehindSource(Entity vfx, Entity source)
    {
        var color = Has<ColorBlend>(source) ? Get<ColorBlend>(source).Color : Color.White;
        color.A /= 2; // transparency

        Set(vfx, new ColorBlend(color));

        Relate(source, vfx, new TrailingVisuals());
        Relate(vfx, source, new Source());
        Set(vfx, new DestroyWhenNoSource());
    }
}