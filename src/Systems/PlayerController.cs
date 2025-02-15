using System;
using Snake.Components;
using Snake.Content;
using Snake.Data;
using Snake.Messages;
using Snake.Relations;
using Snake.Utility;
using MoonTools.ECS;
using MoonWorks.Graphics;
using MoonWorks.Math;
using System.Numerics;
using MoonWorks.Audio;
using System.Runtime.InteropServices;

namespace Snake.Systems;

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
		Vector2 startPos = new Vector2(1, 1);
		World.Set(player, new TilePosition(startPos)); // (0, 0) is occupied by a wall
		World.Set(player, new LastTilePosition(startPos));

		//World.Set(player, new SpriteAnimation(index == 0 ? Content.SpriteAnimations.Char_Walk_Down : Content.SpriteAnimations.Char2_Walk_Down, 0));
		World.Set(player, new SpriteAnimation(Content.SpriteAnimations.Char_Walk_Down, 0));

		World.Set(player, new PlayerIndex(index));
		World.Set(player, new Solid());
		World.Set(player, index == 0 ? Color.Green : Color.Blue);
		World.Set(player, new Depth(5));
		World.Set(player, new MovementTimer(0.5f));
		World.Set(player, new IntegerVelocity(Vector2.Zero));
		World.Set(player, new LastMovedDirection(Vector2.Zero));
		World.Set(player, new CachedDirection(new Vector2(1, 0))); // Move right
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
			var direction = Get<CachedDirection>(entity).Direction;
			{
				Vector2 newDirection;
				if (inputState.Left.IsDown)
				{
					newDirection = new Vector2(-1, 0);
				}
				else if (inputState.Right.IsDown)
				{
					newDirection = new Vector2(1, 0);
				}
				else if (inputState.Up.IsDown)
				{
					newDirection = new Vector2(0, -1); // going up is approaching Y = 0
				}
				else if (inputState.Down.IsDown)
				{
					newDirection = new Vector2(0, 1);
				}
				else {
					newDirection = direction;
				}
				
				// Ignore inputs trying to go backwards
				if (newDirection != -Get<LastMovedDirection>(entity).Direction)
				{
					direction = newDirection;
					Set(entity, new CachedDirection(direction));
				}
			}
			#endregion

			#region Movement
			{
				var moveTimer = Get<MovementTimer>(entity);
				var timeLeft = moveTimer.TimeLeftInSecs - deltaTime;
				if (timeLeft <= 0) 
				{
					Set(entity, new IntegerVelocity(direction));
					Set(entity, new LastMovedDirection(direction));
					// Reset movement timer.
					Set(entity, new MovementTimer(moveTimer.Max));
				}
				else {
					// Store the ticking down.
					Set(entity, new MovementTimer(timeLeft, moveTimer.Max));
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
