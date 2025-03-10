using System;
using System.Numerics;
using System.Threading.Tasks.Dataflow;
using MoonTools.ECS;
using MoonWorks.Graphics;
using MoonWorks.Math;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;
using Filter = MoonTools.ECS.Filter;

// Inspired from Cassandra Lugo's Bullet system from https://blood.church/posts/2023-09-25-shmup-tutorial/
public class Projectile : MoonTools.ECS.System
{
    TargetingIndicatorSpawner TargetingIndicatorSpawner;
    public Filter ProjectileFilter;

    public Projectile(World world) : base(world)
    {
        TargetingIndicatorSpawner = new TargetingIndicatorSpawner(world);

        ProjectileFilter = 
        FilterBuilder
        .Include<SpriteAnimation>()
        .Include<Position>()
        .Include<Direction>()
        .Include<Speed>()
        .Include<DealsDamageOnContact>()
        .Exclude<Player>()
        .Build();
    }

    Entity CreateProjectile(
        Vector2 position, 
        CollisionLayer layer, 
        Vector2 direction, 
        float hitscanSpeed,
        float speed, // ignored if hitscanSpeed > 0
        float maxDistance
        )
    {
        var entity = CreateEntity();
        Set(entity, new SpriteAnimation(SpriteAnimations.Projectile));
        Set(entity, new Position(position));
        Set(entity, new Rectangle(-2, -3, 4, 6));
        Set(entity, new Layer(layer, CollisionLayer.Bullet));
        direction = MathUtilities.SafeNormalize(direction);
        Set(entity, new Direction(direction));
        if (hitscanSpeed > 0)
        {
            Set(entity, new HitscanSpeed(hitscanSpeed));
            Set(entity, new Speed(0f));
        }
        else
        {
            Set(entity, new Speed(speed));
        }
        if (maxDistance > 0)
        {
            Set(entity, new MaxDistance(maxDistance));
        }
        Set(entity, new DealsDamageOnContact(1));
        Set(entity, new DestroyWhenOutOfBounds());
        Set(entity, new DestroyOnCollision());

        var flipTimer = CreateEntity();
        Set(flipTimer, new Timer(1f, true));
        Relate(entity, flipTimer, new WillRotate(0.1f, float.DegreesToRadians(90)));

        return entity;
    }

    void SpawnFriendlinessPellets_Pattern1()
    {
        var spawn_pos = new Vector2(170, 50);
        var x_offset = 80;
        const float Speed = 200f;
        const int NumProjectiles = 5;

        for (int i = 0; i < NumProjectiles; ++i)
        {    
            var waitTime = 1.0f;

            Send(new ShootFromArea(
                spawn_pos, 
                CollisionLayer.EnemyBullet, 
                new Vector2(0f, 1f), 
                0f,
                Speed,
                -1,
                waitTime,
                Some<Player>() ? GetSingletonEntity<Player>() : default
                )
            );

            spawn_pos.X += x_offset;
        }
    }

    void SpawnFriendlinessPellets_HitscanPattern()
    {
        var spawn_pos = new Vector2(170, 50);
        var x_offset = 80;
        const int NumProjectiles = 5;
        const float hitscanSpeed = 2000f;

        for (int i = 0; i < NumProjectiles; ++i)
        {    
            var waitTime = 1.0f;

            Send(new ShootFromArea(
                spawn_pos, 
                CollisionLayer.EnemyBullet, 
                new Vector2(0f, 1f), 
                hitscanSpeed,
                0f,
                -1,
                waitTime,
                Some<Player>() ? GetSingletonEntity<Player>() : default
                )
            );

            spawn_pos.X += x_offset;
        }
    }

    public override void Update(TimeSpan delta)
    {
        if (ProjectileFilter.Empty)
        {
            //SpawnFriendlinessPellets_Pattern1();
            SpawnFriendlinessPellets_HitscanPattern();
        }

        foreach (var message in ReadMessages<ShootFromEntity>())
        {
            var positionComponent = Get<Position>(message.Source);
            var position = new Vector2(positionComponent.X, positionComponent.Y);
            CreateProjectile(position, message.Layer, message.Direction, 0f, message.Speed, -1f);
        }

        foreach (var message in ReadMessages<ShootFromArea>())
        {
            var projectile = CreateProjectile(
                message.Position, 
                message.Layer, 
                message.DelayTime <= 0.0f ? message.Direction : Vector2.Zero, 
                message.HitscanSpeed,
                message.Speed,
                message.MaxDistance
            );

            if (message.DelayTime > 0.0f)
            {
                var attackDelayTimer = CreateEntity();
                Set(attackDelayTimer, new Timer(message.DelayTime));
                Relate(projectile, attackDelayTimer, new SpeedMult(0.0f));
            }

            if (message.Target != default)
            {
                Relate(projectile, message.Target, new TargetingEntity());
                Set(projectile, new UpdateDirectionToTargetPosition(true));
                if (message.HitscanSpeed > 0.0f)
                {
                    var indicator = TargetingIndicatorSpawner.CreateTargetingIndicator(
                        projectile, 
                        message.Target,
                        true,
                        true
                    );
                
                    var color = Color.Salmon;
                    color.A -= 100; // transparency
                    Set(indicator, new ColorBlend(color));
                    // TODO: Make this flicker more noticeable
                    Set(indicator, new ColorFlicker(0, Color.Transparent));
                    Set(indicator, new SpriteAnimation(SpriteAnimations.Pixel));
                    if (message.DelayTime > 0.0f)
                    {
                        var targetingDelayTimer = CreateEntity();
                        Set(targetingDelayTimer, new Timer(message.DelayTime * 0.75f));
                        Relate(targetingDelayTimer, indicator, new DeleteWhenTimerEnds());
                        Relate(projectile, targetingDelayTimer, new DontFollowTarget());
                    }
                }
            }
        }
    }
}