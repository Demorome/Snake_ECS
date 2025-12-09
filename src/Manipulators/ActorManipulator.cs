using System;
using System.ComponentModel;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks.Graphics;
using MoonWorks.Math;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class ActorManipulator : MoonTools.ECS.Manipulator
{
    public ActorManipulator(World world) : base(world)
    {
    }

	public Entity SpawnPlayer(Position2D pos, int index)
	{
		var player = World.CreateEntity($"Player {index}");
		Set(player, new Player(index));
		Set(player, pos);
		//Set(player, new SpriteAnimation(index == 0 ? Content.SpriteAnimations.Char_Walk_Down : Content.SpriteAnimations.Char2_Walk_Down, 0));
		//Set(player, new DrawAsRectangle());
		Set(player, new SpriteAnimation(SpriteAnimations.Heart));
		Set(player, new Rectangle(-12, -12, 24, 24));
		Set(player, new Layer(CollisionLayer.PlayerActor_ExistsOn, CollisionLayer.PlayerActor_CollidesWith));
		Set(player, new CanMoveThroughDespiteCollision(CollisionLayer.Projectile));
		//Set(player, index == 0 ? Color.Green : Color.Blue);
		Set(player, new ColorBlend(Color.Red));
		Set(player, new Depth(DepthLayer.Player));
		Set(player, new MaxSpeed(RollAndCash.Systems.PlayerController.MaxPlayerSpeedBase));
		Set(player, new Speed(0f));
		Set(player, new Direction2D(Vector2.Zero));
		//Set(player, new AdjustFramerateToSpeed());
		Set(player, new RollAndCash.Systems.InputState());
		Set(player, new CursorPosition());
		Set(player, new HasHealth(5));
		Set(player, new BecomeInvincibleOnDamage(1f));
		Set(player, new CanBeDetected());
		Set(player, new VisualScale(new Vector2(1, 1)));

		// For debugging raycasts
		//Set(player, new CanDetect(float.DegreesToRadians(45f), 30f));

		/*
		Set(player, new DirectionalSprites(
			index == 0 ? Content.SpriteAnimations.Char_Walk_Up.ID : Content.SpriteAnimations.Char2_Walk_Up.ID,
			index == 0 ? Content.SpriteAnimations.Char_Walk_UpRight.ID : Content.SpriteAnimations.Char2_Walk_UpRight.ID,
			index == 0 ? Content.SpriteAnimations.Char_Walk_Right.ID : Content.SpriteAnimations.Char2_Walk_Right.ID,
			index == 0 ? Content.SpriteAnimations.Char_Walk_DownRight.ID : Content.SpriteAnimations.Char2_Walk_DownRight.ID,
			index == 0 ? Content.SpriteAnimations.Char_Walk_Down.ID : Content.SpriteAnimations.Char2_Walk_Down.ID,
			index == 0 ? Content.SpriteAnimations.Char_Walk_DownLeft.ID : Content.SpriteAnimations.Char2_Walk_DownLeft.ID,
			index == 0 ? Content.SpriteAnimations.Char_Walk_Left.ID : Content.SpriteAnimations.Char2_Walk_Left.ID,
			index == 0 ? Content.SpriteAnimations.Char_Walk_UpLeft.ID : Content.SpriteAnimations.Char2_Walk_UpLeft.ID
		));*/

		return player;
	}

    public Entity SpawnFrog(Position2D position)
    {
        var entity = CreateEntity("Frog");
    
        Set(entity, position);
        var sprite = SpriteAnimations.NPC_Frog;
        Set(entity, new SpriteAnimation(sprite));
		Set(entity, new Rectangle(-sprite.OriginX, -sprite.OriginY, 32, 32)); // Could use sprite.Frames[0].FrameRect.W
		Set(entity, new Layer(CollisionLayer.EnemyActor_ExistsOn, CollisionLayer.EnemyActor_CollidesWith));
		//Set(entity, new CanMoveThroughDespiteCollision(CollisionLayer.Player));
        Set(entity, new Depth(DepthLayer.Enemy)); // draw just below player (depth 5)
        Set(entity, new DealsDamageOnContact(1));
        Set(entity, new Direction2D(Vector2.Zero));
        Set(entity, new CanDetect(float.DegreesToRadians(45f), 100f));
        Set(entity, new DrawDetectionCone());

		Set(entity, new HasHealth(2));
		
		Set(entity, new DestroyOnTransition());
        
        return entity;
    }
}