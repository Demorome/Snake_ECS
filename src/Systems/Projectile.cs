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
    public Filter PlayerFilter;

    public Projectile(World world) : base(world)
    {
        ProjectileFilter = 
        FilterBuilder
        .Include<SpriteAnimation>()
        .Include<Position>()
        .Include<Direction>()
        .Include<Velocity>()
        .Include<DealsDamageOnContact>()
        .Include<DestroyOnCollision>()
        .Build();

        /*
        PlayerFilter =
        FilterBuilder
        .Include<SpriteAnimation>()
        .Include<Position>()
        .Include<Velocity>()
        .Include<Player>()
        .Build();*/
    }

    void SpawnFriendlinessPellets_Pattern1()
    {
        var spawn_pos = new Vector2(170, 50);
        var x_offset = 80;
        const float Speed = 0f/*200f*/;
        const int NumProjectiles = 5;

        if (!Some<Player>()) return;
        var player = GetSingletonEntity<Player>();
        var playerPosition = Get<Position>(player).AsVector();

        for (int i = 0; i < NumProjectiles; ++i)
        {
            //var angleToPlayer = playerPosition %;
            
            Vector2 projectileDirection = MathUtilities.SafeNormalize(playerPosition - spawn_pos);

            //Vector2 projectileDirection = new Vector2(MathF.Sin(angleToPlayer), MathF.Cos(angleToPlayer));

            Send(new ShootFromArea(spawn_pos, CollisionLayer.EnemyBullet, projectileDirection, Speed));

            spawn_pos.X += x_offset;
        }
    }

    Entity CreateProjectile(Vector2 position, CollisionLayer layer, Vector2 direction, float speed)
    {
        var entity = CreateEntity();
        Set(entity, new SpriteAnimation(SpriteAnimations.Projectile));
        Set(entity, new Position(position));
        Set(entity, new Rectangle(-1, -3, 3, 5));
        Set(entity, new Layer(layer, CollisionLayer.Bullet));
        direction = MathUtilities.SafeNormalize(direction);
        Set(entity, new Direction(direction));
        Set(entity, new Velocity(direction * speed));
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
            CreateProjectile(message.Position, message.Layer, message.Direction, message.Speed);
        }
    }
}