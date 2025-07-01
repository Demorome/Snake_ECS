using System;
using MoonTools.ECS;
using RollAndCash.Relations;
using RollAndCash.Components;
using RollAndCash.Utility;
using System.Numerics;

namespace RollAndCash.Systems;

public class EnemySystem : MoonTools.ECS.System
{
    MoonTools.ECS.Filter EnemyFilter;
    EnemySpawner EnemySpawner;
    ProjectileManipulator ProjectileManipulator;

    public EnemySystem(World world) : base(world)
    {
        EnemyFilter =
            FilterBuilder
            .Include<DealsDamageOnContact>()
            //.Include<CanAttack>()
            .Build();

        EnemySpawner = new(world);
        ProjectileManipulator = new(world);
    }

    public override void Update(TimeSpan delta)
    {
        if (EnemyFilter.Empty)
        {
            var newEnemy = EnemySpawner.SpawnFrog();
        }

        foreach (var entity in EnemyFilter.Entities)
        {
            if (Has<ChargingUpAttack>(entity))
            {
                if (!HasOutRelation<ChargingUpAttackTimer>(entity))
                {
                    Remove<ChargingUpAttack>(entity);
                    var target = OutRelationSingleton<Targeting>(entity);
                    var position = Get<Position>(entity);
                    var targetPos = Get<Position>(target);

                    // TODO: Don't use target position, but rather an outdated position of theirs.
                    // Do the attack
                    var projectile = ProjectileManipulator.CreateProjectile(
                        position.AsVector(),
                        CollisionLayer.EnemyBullet,
                        MathUtilities.SafeNormalize(targetPos - position),
                        2000f,
                        0f,
                        2000f
                    );
                }
            }

            if (HasInRelation<Detected>(entity))
            {
                if (!Has<ChargingUpAttack>(entity))
                {
                    // Start attack timer
                    var targetingTimer = CreateEntity();
                    Set(targetingTimer, new Timer(1f));
                    Relate(entity, targetingTimer, new ChargingUpAttackTimer());

                    Set(entity, new ChargingUpAttack());
                    var other = InRelationSingleton<Detected>(entity);
                    Relate(entity, other, new Targeting());

                    /*
                    var targetingTime = delayTime * 0.75f;
                    FlickeringManipulator.StartFlickering(targetingVisual, targetingTime, 0.2f);
                    Set(targetingVisual, new Timer(targetingTime));
                    Relate(projectile, targetingVisual, new DontFollowTarget());*/
                }
            }
        }
    }
}
