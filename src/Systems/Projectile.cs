using System;
using MoonTools.ECS;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
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
        .Include<Velocity>()
        .Include<DealsDamageOnContact>()
        .Build();
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var message in ReadMessages<Shoot>())
        {
            var position = Get<Position>(message.Source);

            var entity = CreateEntity();
            Set(entity, new SpriteAnimation(SpriteAnimations.Projectile));
            Set(entity, new Position(position.X, position.Y));
            var direction = MathUtilities.SafeNormalize(message.Direction);
            Set(entity, new Direction(direction));
            Set(entity, new Velocity(direction * message.Speed));
            Set(entity, new DealsDamageOnContact());
            Set(entity, new DestroyWhenOutOfBounds());
        }
    }
}