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
using Hexa.NET.ImGui;

namespace RollAndCash.Systems;

public class PlayerController : MoonTools.ECS.System
{
	MoonTools.ECS.Filter PlayerFilter;
	public static float MaxPlayerSpeedBase = 200f;

	ProjectileManipulator ProjectileManipulator;

	public PlayerController(World world) : base(world)
	{
		PlayerFilter =
		FilterBuilder
		.Include<Player>()
		.Include<Position2D>()
		.Include<Speed>()
		.Include<Direction2D>()
		.Include<HasHealth>()
		.Build();

		ProjectileManipulator = new(world);
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
			}

			if (inputState.Attack.IsPressed && !ImGui.GetIO().WantCaptureMouse)
			{
				// Shoot where player is aiming
				var pos = Get<Position2D>(entity);
				var cursorPos = Get<CursorPosition>(entity).Value;

				ProjectileManipulator.CreateProjectile(
					pos,
					new Layer(CollisionLayer.PlayerBullet_ExistsOn, CollisionLayer.PlayerBullet_CollidesWith),
					CollisionLayer.Enemy,
					cursorPos - pos.AsVector(),
					10000f,
					0f,
					2000f
				);
			}
			#endregion

			#region Movement
			var maxSpeed = Get<MaxSpeed>(entity).Value;
			direction = MathUtilities.SafeNormalize(direction);
			//var velocity = direction * maxSpeed;

			Set(entity, new Direction2D(direction));
			Set(entity, new Speed(maxSpeed));

			#endregion
		}
	}
}
