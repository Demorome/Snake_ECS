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

public class ProjectileManipulator : MoonTools.ECS.Manipulator
{
    VFXManipulator VFXManipulator;
    FlickeringManipulator FlickeringManipulator;

    public ProjectileManipulator(World world) : base(world)
    {
        VFXManipulator = new VFXManipulator(world);
        FlickeringManipulator = new FlickeringManipulator(world);
    }

    public Entity CreateProjectile(
        Vector2 position, 
        Layer layer,
        CollisionLayer canMoveThroughLayer,
        Vector2 direction, 
        float hitscanSpeed,
        float speed, // ignored if hitscanSpeed > 0
        float maxDistance,
        bool destroyOnCollision = true
        )
    {
        var entity = CreateEntity();
        Set(entity, new SpriteAnimation(SpriteAnimations.Projectile));
        Set(entity, new Position(position));
        Set(entity, new Rectangle(-2, -3, 4, 6));

        Set(entity, layer);
        if (canMoveThroughLayer != CollisionLayer.None)
        {
            Set(entity, new CanMoveThroughDespiteCollision(canMoveThroughLayer));
        }

        direction = MathUtilities.SafeNormalize(direction);
        Set(entity, new Direction(direction));

        if (hitscanSpeed > 0)
        {
            Set(entity, new HitscanSpeed(hitscanSpeed));
            Set(entity, new Speed(0f)); // to satisfy filters
        }
        else
        {
            Set(entity, new Speed(speed));
        }
        if (maxDistance > 0)
        {
            Set(entity, new MaxMovementDistance(maxDistance));
        }

        Set(entity, new DealsDamageOnContact(1));
        Set(entity, new DestroyWhenOutOfBounds());

        if (destroyOnCollision)
        {
            Set(entity, new DestroyOnImpact());
        }

        return entity;
    }

    // Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
    // Refactored using manipulator pattern instead of message pattern.
    // For Undertale-style attacks that are fired from the void.
    public Entity ShootFromArea(
        Vector2 position,
        Layer layer,
        CollisionLayer canMoveThroughLayer,
        Vector2 direction,
        float hitscanSpeed,
        float speed, // ignored if HitscanSpeed > 0
        float maxDistance,
        float delayTime = 0f,
        Entity? target = null
    )
    {
        var projectile = CreateProjectile(
            position,
            layer,
            canMoveThroughLayer,
            delayTime <= 0.0f ? direction : Vector2.Zero,
            hitscanSpeed,
            speed,
            maxDistance
        );

        if (delayTime > 0.0f)
        {
            var attackDelayTimer = CreateEntity();
            Set(attackDelayTimer, new Timer(delayTime));
            Relate(projectile, attackDelayTimer, new SpeedMult(0.0f));
            //Relate(projectile, attackDelayTimer, new DelayedAttack()); // TODO: Send ProjectileAttack message
        }

        if (target.HasValue)
        {
            Relate(projectile, target.Value, new Targeting());
            Set(projectile, new UpdateDirectionToTargetPosition(true));
            if (hitscanSpeed > 0.0f)
            {
                var projectilePos = Get<Position>(projectile).AsVector();
                var projectileSprite = Get<SpriteAnimation>(projectile);

                // Create targeting visual
                Entity targetingVisual = VFXManipulator.CreateVFX(projectilePos, projectileSprite, -1f);
                VFXManipulator.MakeVFXFollowSource(targetingVisual, projectile);
                VFXManipulator.MakeVFXPointAtTarget(targetingVisual, target.Value, true, true);

                var color = Color.Salmon;
                color.A -= 100; // transparency
                Set(targetingVisual, new ColorBlend(color));
                //Set(indicator, new ColorFlicker(0, Color.Transparent));
                Set(targetingVisual, new SpriteAnimation(SpriteAnimations.Pixel));
                if (delayTime > 0.0f)
                {
                    var targetingTime = delayTime * 0.75f;
                    FlickeringManipulator.StartFlickering(targetingVisual, targetingTime, 0.2f);

                    Set(targetingVisual, new Timer(targetingTime));
                    Relate(projectile, targetingVisual, new DontFollowTarget());
                }
            }
        }

        return projectile;
    }

    Entity ShootFriendlinessPellet(
        Vector2 position,
        Layer layer,
        CollisionLayer canMoveThroughLayer,
        Vector2 direction,
        float hitscanSpeed,
        float speed, // ignored if HitscanSpeed > 0
        float maxDistance,
        float delayTime = 0f,
        Entity? target = null
    )
    {
        var proj = ShootFromArea(
            position,
            layer,
            canMoveThroughLayer,
            direction,
            hitscanSpeed,
            speed,
            maxDistance,
            delayTime,
            target
        );

        // Manual spinning animation.
        var flipTimer = CreateEntity();
        Set(flipTimer, new Timer(1f, true));
        Relate(proj, flipTimer, new WillRotate(0.1f, float.DegreesToRadians(90)));

        return proj;
    }

    void SpawnFriendlinessPellets_Top_Pattern1()
    {
        var spawn_pos = new Vector2(170, 50);
        var x_offset = 80;
        const float Speed = 200f;
        const int NumProjectiles = 5;

        for (int i = 0; i < NumProjectiles; ++i)
        {    
            var waitTime = 1.0f;

            var pellet = ShootFriendlinessPellet(
                spawn_pos,
                new Layer(CollisionLayer.EnemyBullet_ExistsOn, CollisionLayer.EnemyBullet_CollidesWith),
                CollisionLayer.None,
                new Vector2(0f, 1f),
                0f,
                Speed,
                -1,
                waitTime,
                Some<Player>() ? GetSingletonEntity<Player>() : null
            );

            spawn_pos.X += x_offset;
        }
    }

    void SpawnFriendlinessPellets_Top_HitscanPattern()
    {
        var spawn_pos = new Vector2(170, 50);
        var x_offset = 80;
        const int NumProjectiles = 5;
        const float hitscanSpeed = 2000f;

        for (int i = 0; i < NumProjectiles; ++i)
        {
            var waitTime = 1.0f;

            var pellet = ShootFriendlinessPellet(
                spawn_pos,
                new Layer(CollisionLayer.EnemyBullet_ExistsOn, CollisionLayer.EnemyBullet_CollidesWith),
                CollisionLayer.None,
                new Vector2(0f, 1f),
                hitscanSpeed,
                0f,
                -1,
                waitTime,
                Some<Player>() ? GetSingletonEntity<Player>() : null
            );

            spawn_pos.X += x_offset;
        }
    }

    void SpawnFriendlinessPellets_Left_HitscanPattern()
    {
        var spawn_pos = new Vector2(130, 100);
        var y_offset = 40;
        const int NumProjectiles = 5;
        const float hitscanSpeed = 2000f;

        for (int i = 0; i < NumProjectiles; ++i)
        {    
            var waitTime = 1.0f;

            var pellet = ShootFriendlinessPellet(
                spawn_pos,
                new Layer(CollisionLayer.EnemyBullet_ExistsOn, CollisionLayer.EnemyBullet_CollidesWith),
                CollisionLayer.None,
                new Vector2(0f, 1f),
                hitscanSpeed,
                0f,
                -1,
                waitTime,
                Some<Player>() ? GetSingletonEntity<Player>() : null
            );

            spawn_pos.Y += y_offset;
        }
    }
}