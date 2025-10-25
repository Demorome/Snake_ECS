using System;
using System.ComponentModel;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class MirrorManipulator : MoonTools.ECS.Manipulator
{

    public MirrorManipulator(World world) : base(world)
    {
    }

    public Entity CreateMirror(
        // NOTE: NOT centered w/ the mirror's length!
        // startPosition gives us one end of the mirror, and the other can be found w/ the direction and length.
        // This is so we can easily calculate line vs line collisions later on.
        Position2D startPosition,
        Layer layer,
        Vector2 direction,
        int length,
        int visualWidth = 1  // NOTE: Doesn't impact the collision hitbox!
        )
    {
        var entity = CreateEntity("Mirror");
        Set(entity, new SpriteAnimation(SpriteAnimations.Pixel));
        Set(entity, new ColorBlend(Color.DeepSkyBlue));

        Set(entity, startPosition);
        Set(entity, layer);

        direction = MathUtilities.SafeNormalize(direction);
        Set(entity, new Direction2D(direction));
        Set(entity, new RotatesWithDirection());

        Set(entity, new SpriteScale(new Vector2(length, visualWidth)));

        Set(entity, new HasLineHitbox());
        //Set(entity, new ReflectsProjectiles()); // TODO: Reflect layer component instead?

        return entity;
    }

    #region Prefabs

    public Entity CreateStaticLevelMirror(Position2D pos)
    {
        return CreateMirror(
            pos,
            new Layer(CollisionLayer.Level, CollisionLayer.Bullet),
            new Vector2(1, 0), 30, 1
        );
    }
    
    
    #endregion Prefabs
}