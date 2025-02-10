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
using MoonWorks.Audio;
using System.Runtime.InteropServices;

namespace RollAndCash.Systems;

public class PlayerController : MoonTools.ECS.System
{
	MoonTools.ECS.Filter PlayerFilter;

	public PlayerController(World world) : base(world)
	{
		PlayerFilter =
		FilterBuilder
		.Include<PlayerIndex>()
		.Include<TilePosition>()
		.Build();
	}

	public Entity SpawnPlayer(int index)
	{
		var player = World.CreateEntity();

		//World.Set(player, new Position(Dimensions.GAME_W * 0.47f + index * 48.0f, Dimensions.GAME_H * 0.25f));
		World.Set(player, new TilePosition(0, 0));

		//World.Set(player, new SpriteAnimation(index == 0 ? Content.SpriteAnimations.Char_Walk_Down : Content.SpriteAnimations.Char2_Walk_Down, 0));
		World.Set(player, new SpriteAnimation(Content.SpriteAnimations.Char_Walk_Down, 0));

		World.Set(player, new PlayerIndex(index));
		World.Set(player, new Rectangle(-8, -8, 16, 16));
		World.Set(player, new Solid());
		World.Set(player, index == 0 ? Color.Green : Color.Blue);
		World.Set(player, new Depth(5));
		World.Set(player, new MovementTimer(0.5f));
		World.Set(player, new LastDirection(Vector2.Zero));
		//World.Set(player, new AdjustFramerateToSpeed());
		World.Set(player, new InputState());
		/*
		World.Set(player, new DirectionalSprites(
			index == 0 ? Content.SpriteAnimations.Char_Walk_Up.ID : Content.SpriteAnimations.Char2_Walk_Up.ID,
			index == 0 ? Content.SpriteAnimations.Char_Walk_Right.ID : Content.SpriteAnimations.Char2_Walk_Right.ID,
			index == 0 ? Content.SpriteAnimations.Char_Walk_Down.ID : Content.SpriteAnimations.Char2_Walk_Down.ID,
			index == 0 ? Content.SpriteAnimations.Char_Walk_Left.ID : Content.SpriteAnimations.Char2_Walk_Left.ID,
		));*/
		World.Set(player, new DirectionalSprites(
			Content.SpriteAnimations.Char_Walk_Up.ID,
			Content.SpriteAnimations.Char_Walk_Right.ID,
			Content.SpriteAnimations.Char_Walk_Down.ID,
			Content.SpriteAnimations.Char_Walk_Left.ID
		));

		return player;
	}

	public override void Update(System.TimeSpan delta)
	{
		if (!Some<GameInProgress>()) { return; }

		var deltaTime = (float)delta.TotalSeconds;

		foreach (var entity in PlayerFilter.Entities)
		{
			//var playerIndex = Get<PlayerIndex>(entity).Index;

			#region Input
			var inputState = Get<InputState>(entity);
			var direction = Get<LastDirection>(entity).Direction;
			{
				var newDirection = Vector2.Zero;
				if (inputState.Left.IsDown)
				{
					newDirection.X = -1;
				}
				else if (inputState.Right.IsDown)
				{
					newDirection.X = 1;
				}
				else if (inputState.Up.IsDown)
				{
					newDirection.Y = -1;
				}
				else if (inputState.Down.IsDown)
				{
					newDirection.Y = 1;
				}
				
				// Ignore inputs trying to go backwards
				if (newDirection != direction)
				{
					direction = newDirection;
				}
			}
			#endregion

			#region Movement
			{
				var moveTimer = Get<MovementTimer>(entity);
				var timeLeft = moveTimer.TimeLeftInSecs - deltaTime;
				if (timeLeft <= 0) 
				{
					var curPos = Get<TilePosition>(entity).PositionVector;
					Set(entity, new TilePosition(curPos + direction));
					// Reset movement timer.
					Set(entity, new MovementTimer(moveTimer.Max));
				}
			}
			#endregion


			// #region walking sfx
			// if (!HasOutRelation<TimingFootstepAudio>(entity) && framerate > 0)
			// {
			// 	PlayRandomFootstep();

			// 	var footstepTimer = World.CreateEntity();
			// 	var footstepDuration = Math.Clamp(1f - (framerate / 50f), .5f, 1f);
			// 	Set(footstepTimer, new Timer(footstepDuration));
			// 	World.Relate(entity, footstepTimer, new TimingFootstepAudio());
			// }
			// #endregion
		}
	}

	/*
	private void PlayRandomFootstep()
	{
		Send(
			new PlayStaticSoundMessage(
				new StaticSoundID[]
				{
					StaticAudio.Footstep1,
					StaticAudio.Footstep2,
					StaticAudio.Footstep3,
					StaticAudio.Footstep4,
					StaticAudio.Footstep5,
				}.GetRandomItem<StaticSoundID>(),
			SoundCategory.Generic,
			Rando.Range(0.66f, 0.88f),
			Rando.Range(-.05f, .05f)
			)
		);
	}*/
}
