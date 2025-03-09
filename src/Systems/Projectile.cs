using System;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks.Math;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

// Inspired from Cassandra Lugo's Bullet system from https://blood.church/posts/2023-09-25-shmup-tutorial/
public class Projectile : MoonTools.ECS.System
{
    public Filter ProjectileFilter;

    public Projectile(World world) : base(world)
    {
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
                Speed,
                waitTime,
                Some<Player>() ? GetSingletonEntity<Player>() : default
                )
            );

            spawn_pos.X += x_offset;
        }
    }

    Entity CreateProjectile(Vector2 position, CollisionLayer layer, Vector2 direction, float speed)
    {
        var entity = CreateEntity();
        Set(entity, new SpriteAnimation(SpriteAnimations.Projectile));
        Set(entity, new Position(position));
        Set(entity, new Rectangle(-2, -3, 4, 6));
        Set(entity, new Layer(layer, CollisionLayer.Bullet));
        direction = MathUtilities.SafeNormalize(direction);
        Set(entity, new Direction(direction));
        Set(entity, new Speed(speed));
        Set(entity, new DealsDamageOnContact(1));
        Set(entity, new DestroyWhenOutOfBounds());
        Set(entity, new DestroyOnCollision());

        var flipTimer = CreateEntity();
        Set(flipTimer, new Timer(1f, true));
        Relate(entity, flipTimer, new WillRotate(0.1f, float.DegreesToRadians(90)));

        return entity;
    }

    public override void Update(TimeSpan delta)
    {
        if (ProjectileFilter.Empty)
        {
            SpawnFriendlinessPellets_Pattern1();
        }

        foreach (var message in ReadMessages<ShootFromEntity>())
        {
            var positionComponent = Get<Position>(message.Source);
            var position = new Vector2(positionComponent.X, positionComponent.Y);
            CreateProjectile(position, message.Layer, message.Direction, message.Speed);
        }

        foreach (var message in ReadMessages<ShootFromArea>())
        {
            var projectile = CreateProjectile(
                message.Position, 
                message.Layer, 
                message.DelayTime <= 0.0f ? message.Direction : Vector2.Zero, 
                message.Speed
            );

            if (message.DelayTime > 0.0f)
            {
                var timerEntity = CreateEntity();
                Set(timerEntity, new Timer(message.DelayTime));
                Relate(projectile, timerEntity, new SpeedMult(0.0f));

                if (message.DelayedTarget != default)
                {
                    Relate(projectile, message.DelayedTarget, new Targeting());
                    Set(projectile, new UpdateDirectionToTargetPosition(true, true));
                }
            }
        }

        foreach (var entity in ProjectileFilter.Entities)
        {
            if (Has<UpdateDirectionToTargetPosition>(entity))
            {
                if (HasOutRelation<Targeting>(entity))
                {
                    var targetEntity = OutRelationSingleton<Targeting>(entity);
                    var speed = Get<Speed>(entity).Value;
                    var updateData = Get<UpdateDirectionToTargetPosition>(entity);
                    if (!updateData.OnlyIfSpeedNonZero || speed != 0.0f)
                    {
                        var targetPosition = Get<Position>(targetEntity).AsVector();
                        var projectilePosition = Get<Position>(entity).AsVector();
                        Vector2 projectileDirection = MathUtilities.SafeNormalize(targetPosition - projectilePosition);
                        //Vector2 projectileDirection = new Vector2(MathF.Sin(angleToPlayer), MathF.Cos(angleToPlayer));
                        Set(entity, new Direction(projectileDirection));

                        if (updateData.DoOnce)
                        {
                            Remove<UpdateDirectionToTargetPosition>(entity);
                        }
                    }
                }
                /*
                else
                {
                    Remove<UpdateDirectionToTargetPosition>(entity);
                }*/
            }
        }
    }
}