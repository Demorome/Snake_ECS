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
    
    public EnemySystem(World world) : base(world)
    {
        EnemyFilter =
            FilterBuilder
            .Include<DealsDamageOnContact>()
            //.Include<CanAttack>()
            .Build();

        EnemySpawner = new(world);
    }

    public override void Update(TimeSpan delta)
    {
        if (EnemyFilter.Empty)
        {
            var newEnemy = EnemySpawner.SpawnFrog();
        }

        /*foreach (var entity in EnemyFilter.Entities)
        {

        }*/
    }
}
