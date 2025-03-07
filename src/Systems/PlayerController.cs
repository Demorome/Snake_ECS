using System;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Data;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;
using MoonTools.ECS;
using MoonWorks.Graphics;
using MoonWorks.Math;
using System.Numerics;

namespace RollAndCash.Systems;

public class PlayerController : MoonTools.ECS.System
{
	MoonTools.ECS.Filter PlayerFilter;
	float MaxSpeedBase = 200f;

	public PlayerController(World world) : base(world)
	{
		PlayerFilter =
		FilterBuilder
		.Include<Player>()
		.Include<Position>()
		.Build();
	}

	public Entity SpawnPlayer(int index)
	{
		var player = World.CreateEntity();
		Set(player, new Position(Dimensions.GAME_W / 2, Dimensions.GAME_H / 2));
		//Set(player, new SpriteAnimation(index == 0 ? Content.SpriteAnimations.Char_Walk_Down : Content.SpriteAnimations.Char2_Walk_Down, 0));
		Set(player, new SpriteAnimation(Content.SpriteAnimations.Heart));
		Set(player, new Player(index));
		Set(player, new Rectangle(0, 0, 24, 24));
		Set(player, new Layer(CollisionLayer.PlayerActor));
		//Set(player, index == 0 ? Color.Green : Color.Blue);
		Set(player, new ColorBlend(Color.Red));
		Set(player, new Depth(5));
		Set(player, new MaxSpeed(MaxSpeedBase));
		Set(player, new Velocity(Vector2.Zero));
		//Set(player, new LastDirection(Vector2.Zero));
		//Set(player, new AdjustFramerateToSpeed());
		Set(player, new InputState());
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

	public override void Update(System.TimeSpan delta)
	{
		if (!Some<GameInProgress>()) { return; }

		var deltaTime = (float)delta.TotalSeconds;

		foreach (var entity in PlayerFilter.Entities)
		{
			//var playerIndex = Get<Player>(entity).Index;
			var direction = Vector2.Zero;

			#region Input
			var inputState = Get<InputState>(entity);

			if (inputState.Left.IsDown)
			{
				direction.X = -1;
			}
			else if (inputState.Right.IsDown)
			{
				direction.X = 1;
			}

			if (inputState.Up.IsDown)
			{
				direction.Y = -1;
			}
			else if (inputState.Down.IsDown)
			{
				direction.Y = 1;
			}

			if (inputState.Interact.IsPressed)
			{
				//Set(entity, new TryHold());
			}
			#endregion

			#region Movement
			var maxSpeed = Get<MaxSpeed>(entity).Value;
			direction = MathUtilities.SafeNormalize(direction);
			var velocity = direction * maxSpeed;

			Set(entity, new Velocity(velocity));

			#endregion
		}
	}
}
