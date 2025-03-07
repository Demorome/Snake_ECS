using System;
using System.ComponentModel;
using MoonTools.ECS;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

// Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
public class Health : MoonTools.ECS.System
{
    public Filter HealthFilter;

    public Health(World world) : base(world)
    {
        HealthFilter = 
        FilterBuilder
        .Include<HasHealth>()
        .Build();
    }

    public override void Update(System.TimeSpan delta)
    {
        foreach (var entity in HealthFilter.Entities)
        {
            if (HasInRelation<Invincible>(entity))
            {
                continue;
            }

            var hp = Get<HasHealth>(entity).Health;
            foreach (var message in ReadMessages<DealDamage>())
            {
                if (message.Target != entity) continue;

                hp -= message.Damage;
                hp = Math.Max(0, hp);

                if (Has<BecomeInvincibleOnDamage>(entity))
                {
                    var timer = CreateEntity();
                    Set(timer, new Timer(Get<BecomeInvincibleOnDamage>(entity).Time));
                    Relate(timer, entity, new Invincible());
                }
            }

            if (hp <= 0)
            {
                Set(entity, new MarkedForDestroy());
            }

            Set(entity, new HasHealth(hp));
        }
    }

}