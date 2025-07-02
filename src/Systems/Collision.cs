using System;
using System.ComponentModel;
using MoonTools.ECS;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

// Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
public class Collision : MoonTools.ECS.System
{
    public Collision(World world) : base(world)
    {
    }

    void HandleCollision(Entity A, Entity B)
    {
        if (Has<HasHealth>(A) && Has<DealsDamageOnContact>(B))
        {
            Send(new DealDamage(A, Get<DealsDamageOnContact>(B).Damage));
        }
        if (Has<HasHealth>(B) && Has<DealsDamageOnContact>(A))
        {
            Send(new DealDamage(B, Get<DealsDamageOnContact>(A).Damage));
        }

        bool impacted = true; // if it stopped when colliding
        if (Has<CanMoveThroughDespiteCollision>(A))
        {
            var canMoveThroughLayer = Get<CanMoveThroughDespiteCollision>(A).Value;
            var otherLayer = Get<Layer>(B).ExistsOn;
            if ((canMoveThroughLayer & otherLayer) != 0)
            {
                impacted = false;
            }
        }

        if (impacted)
        {
            if (Has<DestroyOnImpact>(A))
            {
                Set(A, new MarkedForDestroy());
            }
            if (Has<DestroyOnImpact>(B))
            {
                Set(B, new MarkedForDestroy());
            }
        }
    }

    public override void Update(System.TimeSpan delta)
    {
        foreach (var message in ReadMessages<Collide>())
        {
            HandleCollision(message.A, message.B);
        }
    }

}