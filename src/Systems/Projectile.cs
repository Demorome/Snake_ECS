using System;
using System.Numerics;
using System.Threading.Tasks.Dataflow;
using MoonTools.ECS;
using MoonWorks.Graphics;
using MoonWorks.Math;
using RollAndCash;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;
using Filter = MoonTools.ECS.Filter;

// Inspired from Cassandra Lugo's Bullet system from https://blood.church/posts/2023-09-25-shmup-tutorial/
public class Projectile : MoonTools.ECS.System
{
    VFXManipulator VFXManipulator;
    ProjectileManipulator ProjectileManipulator;
    public Filter ProjectileFilter;

    public Projectile(World world) : base(world)
    {
        VFXManipulator = new VFXManipulator(world);

        ProjectileFilter = FilterBuilder
        .Include<SpriteAnimation>()
        .Include<Position2D>()
        .Include<Direction2D>()
        .Include<Speed>()
        .Include<DealsDamageOnContact>()
        .Exclude<Player>()
        .Build();
    }

    public override void Update(TimeSpan delta)
    {
        if (ProjectileFilter.Empty)
        {
            //ProjectileManipulator.SpawnFriendlinessPellets_Top_Pattern1();
            //ProjectileManipulator.SpawnFriendlinessPellets_Top_HitscanPattern();
            //ProjectileManipulator.SpawnFriendlinessPellets_Left_HitscanPattern();

            /*
            var center_pos = new Vector2(Dimensions.GAME_W / 2 + 50, Dimensions.GAME_H / 2);
            Send(new ShootFromArea(
                center_pos, 
                CollisionLayer.EnemyBullet, 
                new Vector2(0f, 1f), 
                5000f,
                0f,
                -1,
                1f,
                Some<Player>() ? GetSingletonEntity<Player>() : default
                )
            );*/
        }

        foreach (var projectile in ProjectileFilter.Entities)
        {
            
        }
    }
}