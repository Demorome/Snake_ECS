using MonoGame.Extended;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Input;
using RollAndCash.Components;
using System;
using System.Numerics;

namespace RollAndCash.Systems;

public struct InputState
{
	public ButtonState Left { get; set; }
	public ButtonState Right { get; set; }
	public ButtonState Up { get; set; }
	public ButtonState Down { get; set; }
	public ButtonState Interact { get; set; }
	public ButtonState Attack { get; set; }
}

public class ControlSet
{
	public VirtualButton Left { get; set; } = new EmptyButton();
	public VirtualButton Right { get; set; } = new EmptyButton();
	public VirtualButton Up { get; set; } = new EmptyButton();
	public VirtualButton Down { get; set; } = new EmptyButton();
	public VirtualButton Interact { get; set; } = new EmptyButton();
	public VirtualButton Attack { get; set; } = new EmptyButton();
}

public class Input : MoonTools.ECS.System
{
	Inputs Inputs { get; }

	Filter PlayerFilter { get; }

	ControlSet PlayerOneMouseAndKeyboard = new ControlSet();
	ControlSet PlayerOneGamepad = new ControlSet();
	ControlSet PlayerTwoKeyboard = new ControlSet();
	ControlSet PlayerTwoGamepad = new ControlSet();

	OrthographicCamera Camera;

#if DEBUG
	public static Position2D WorldMousePosition = new Position2D();
#endif

	public Input(
		World world,
		Inputs inputs, 
		OrthographicCamera camera
		) : base(world)
	{
#if DEBUG
		Camera = camera;
#endif

		Inputs = inputs;
		PlayerFilter 
			= FilterBuilder
			.Include<Player>()
			.Build();

		PlayerOneMouseAndKeyboard.Up = Inputs.Keyboard.Button(KeyCode.W);
		PlayerOneMouseAndKeyboard.Down = Inputs.Keyboard.Button(KeyCode.S);
		PlayerOneMouseAndKeyboard.Left = Inputs.Keyboard.Button(KeyCode.A);
		PlayerOneMouseAndKeyboard.Right = Inputs.Keyboard.Button(KeyCode.D);
		PlayerOneMouseAndKeyboard.Interact = Inputs.Keyboard.Button(KeyCode.Space);
		PlayerOneMouseAndKeyboard.Attack = Inputs.Mouse.LeftButton;

		PlayerOneGamepad.Up = Inputs.GetGamepad(0).LeftYDown;
		PlayerOneGamepad.Down = Inputs.GetGamepad(0).LeftYUp;
		PlayerOneGamepad.Left = Inputs.GetGamepad(0).LeftXLeft;
		PlayerOneGamepad.Right = Inputs.GetGamepad(0).LeftXRight;
		PlayerOneGamepad.Interact = Inputs.GetGamepad(0).South;
		PlayerOneGamepad.Interact = Inputs.GetGamepad(0).RightShoulder;

		/*PlayerTwoKeyboard.Up = Inputs.Keyboard.Button(KeyCode.Up);
		PlayerTwoKeyboard.Down = Inputs.Keyboard.Button(KeyCode.Down);
		PlayerTwoKeyboard.Left = Inputs.Keyboard.Button(KeyCode.Left);
		PlayerTwoKeyboard.Right = Inputs.Keyboard.Button(KeyCode.Right);
		PlayerTwoKeyboard.Interact = Inputs.Keyboard.Button(KeyCode.Return);

		PlayerTwoGamepad.Up = Inputs.GetGamepad(1).LeftYDown;
		PlayerTwoGamepad.Down = Inputs.GetGamepad(1).LeftYUp;
		PlayerTwoGamepad.Left = Inputs.GetGamepad(1).LeftXLeft;
		PlayerTwoGamepad.Right = Inputs.GetGamepad(1).LeftXRight;
		PlayerTwoGamepad.Interact = Inputs.GetGamepad(1).South;*/
	}

	public override void Update(TimeSpan timeSpan)
	{
		// FIXME: Use a more accurate formula?
		/*var mouseWorldPosition = new Vector2(
			(Inputs.Mouse.X + 0.5f) * ((float)Dimensions.GAME_W / MainWindow.Width),
			(Inputs.Mouse.Y + 0.5f) * ((float)Dimensions.GAME_H / MainWindow.Height)
		);*/

		var mouseWorldPosition = Camera.ScreenToWorld(
			Inputs.Mouse.X, 
			Inputs.Mouse.Y
		);

#if DEBUG
		WorldMousePosition = new Position2D(mouseWorldPosition);
#endif

		foreach (var playerEntity in PlayerFilter.Entities)
		{
			var index = Get<Player>(playerEntity).Index;
			var controlSet = index == 0 ? PlayerOneMouseAndKeyboard : PlayerTwoKeyboard;
			var altControlSet = index == 0 ? PlayerOneGamepad : PlayerTwoGamepad;

			InputState inputState = InputState(controlSet, altControlSet);

			Set(playerEntity, inputState);

			if (!GameStates.GameplayState.LockingCursorPosition)
			{
				// FIXME: Account for potential camera changes (zoom in, etc)
                Set(playerEntity, new CursorPosition(mouseWorldPosition));
            }
		}
	}

	private static InputState InputState(ControlSet controlSet, ControlSet altControlSet)
	{
		return new InputState
		{
			Left = controlSet.Left.State | altControlSet.Left.State,
			Right = controlSet.Right.State | altControlSet.Right.State,
			Up = controlSet.Up.State | altControlSet.Up.State,
			Down = controlSet.Down.State | altControlSet.Down.State,
			Interact = controlSet.Interact.State | altControlSet.Interact.State,
			Attack = controlSet.Attack.State | altControlSet.Attack.State
		};
	}
}
