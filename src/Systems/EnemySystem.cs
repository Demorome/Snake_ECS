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
    VFXManipulator VFXManipulator;

    bool InitDone = false;

    public EnemySystem(World world) : base(world)
    {
        EnemyFilter =
            FilterBuilder
            .Include<DealsDamageOnContact>()
            //.Include<CanAttack>()
            .Build();

        EnemySpawner = new(world);
        ProjectileManipulator = new(world);
        VFXManipulator = new(world);
    }

    public override void Update(TimeSpan delta)
    {
        if (!InitDone)
        {
            var newEnemy = EnemySpawner.SpawnFrog();
            InitDone = true;
        }

        foreach (var entity in EnemyFilter.Entities)
        {
            if (Has<ChargingUpAttack>(entity))
            {
                if (!HasOutRelation<ChargingUpAttackTimer>(entity))
                {
                    Remove<ChargingUpAttack>(entity);
                    if (HasOutRelation<Targeting>(entity))
                    {
                        var target = OutRelationSingleton<Targeting>(entity);
                        var position = Get<Position2D>(entity);
                        var targetPos = Get<Position2D>(target);

                        // TODO: Don't use target position, but rather an outdated position of theirs(?)
                        // Do the attack
                        var projectile = ProjectileManipulator.CreateProjectile(
                            position,
                            new Layer(CollisionLayer.EnemyBullet_ExistsOn, CollisionLayer.EnemyBullet_CollidesWith),
                            CollisionLayer.Player,
                            MathUtilities.SafeNormalize(targetPos - position),
                            10000f,
                            0f,
                            10000f
                        );
                    }
                }
            }

            if (HasInRelation<Detected>(entity))
            {
                if (!Has<ChargingUpAttack>(entity))
                {
                    var other = InRelationSingleton<Detected>(entity);

                    // Start attack timer
                    var targetingVisual = VFXManipulator.CreateTargetingVisual(entity, other);
                    Set(targetingVisual, new Timer(1f));
                    Relate(entity, targetingVisual, new ChargingUpAttackTimer());

                    Set(entity, new ChargingUpAttack());
                    Relate(entity, other, new Targeting()); // FIXME: Remove Targeting at some point?
                }
            }
        }
    }
}
