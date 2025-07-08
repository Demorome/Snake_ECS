using System;
using System.ComponentModel;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks.Math;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class EnemySpawner : MoonTools.ECS.Manipulator
{
    public EnemySpawner(World world) : base(world)
    {
    }

    public Entity SpawnFrog()
    {
        var entity = CreateEntity();
    
        Set(entity, new Position2D(200, 150));
        var sprite = SpriteAnimations.NPC_Frog;
        Set(entity, new SpriteAnimation(sprite));
		Set(entity, new Rectangle(-sprite.OriginX, -sprite.OriginY, 32, 32)); // Could use sprite.Frames[0].FrameRect.W
		Set(entity, new Layer(CollisionLayer.EnemyActor_ExistsOn, CollisionLayer.EnemyActor_CollidesWith));
		//Set(entity, new CanMoveThroughDespiteCollision(CollisionLayer.Player));
        Set(entity, new Depth(6)); // draw just below player (depth 5)
        Set(entity, new DealsDamageOnContact(1));
        Set(entity, new Direction2D(Vector2.Zero));
        Set(entity, new CanDetect(float.DegreesToRadians(45f), 100f));
        Set(entity, new DrawDetectionCone());

        Set(entity, new HasHealth(2));
        
        return entity;
    }
}