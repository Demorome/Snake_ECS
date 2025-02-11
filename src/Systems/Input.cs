using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Input;
using Snake.Components;
using System;

namespace Snake.Systems;

public struct InputState
{
	public ButtonState Left { get; set; }
	public ButtonState Right { get; set; }
	public ButtonState Up { get; set; }
	public ButtonState Down { get; set; }
}

public class ControlSet
{
	public VirtualButton Left { get; set; } = new EmptyButton();
	public VirtualButton Right { get; set; } = new EmptyButton();
	public VirtualButton Up { get; set; } = new EmptyButton();
	public VirtualButton Down { get; set; } = new EmptyButton();
}

public class Input : MoonTools.ECS.System
{
	Inputs Inputs { get; }

	Filter PlayerFilter { get; }

	ControlSet PlayerOneKeyboard = new ControlSet();
	ControlSet PlayerOneGamepad = new ControlSet();
	ControlSet PlayerTwoKeyboard = new ControlSet();
	ControlSet PlayerTwoGamepad = new ControlSet();

	GameLoopManipulator GameLoopManipulator;

	public Input(World world, Inputs inputs) : base(world)
	{
		Inputs = inputs;
		PlayerFilter = FilterBuilder.Include<PlayerIndex>().Build();

		GameLoopManipulator = new GameLoopManipulator(world);

		PlayerOneKeyboard.Up = Inputs.Keyboard.Button(KeyCode.W);
		PlayerOneKeyboard.Down = Inputs.Keyboard.Button(KeyCode.S);
		PlayerOneKeyboard.Left = Inputs.Keyboard.Button(KeyCode.A);
		PlayerOneKeyboard.Right = Inputs.Keyboard.Button(KeyCode.D);

		PlayerOneGamepad.Up = Inputs.GetGamepad(0).LeftYDown;
		PlayerOneGamepad.Down = Inputs.GetGamepad(0).LeftYUp;
		PlayerOneGamepad.Left = Inputs.GetGamepad(0).LeftXLeft;
		PlayerOneGamepad.Right = Inputs.GetGamepad(0).LeftXRight;

		PlayerTwoKeyboard.Up = Inputs.Keyboard.Button(KeyCode.Up);
		PlayerTwoKeyboard.Down = Inputs.Keyboard.Button(KeyCode.Down);
		PlayerTwoKeyboard.Left = Inputs.Keyboard.Button(KeyCode.Left);
		PlayerTwoKeyboard.Right = Inputs.Keyboard.Button(KeyCode.Right);

		PlayerTwoGamepad.Up = Inputs.GetGamepad(1).LeftYDown;
		PlayerTwoGamepad.Down = Inputs.GetGamepad(1).LeftYUp;
		PlayerTwoGamepad.Left = Inputs.GetGamepad(1).LeftXLeft;
		PlayerTwoGamepad.Right = Inputs.GetGamepad(1).LeftXRight;
	}

	public override void Update(TimeSpan timeSpan)
	{
		foreach (var playerEntity in PlayerFilter.Entities)
		{
			var index = Get<PlayerIndex>(playerEntity).Index;
			var controlSet = index == 0 ? PlayerOneKeyboard : PlayerTwoKeyboard;
			var altControlSet = index == 0 ? PlayerOneGamepad : PlayerTwoGamepad;

			InputState inputState = InputState(controlSet, altControlSet);

			Set(playerEntity, inputState);
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
		};
	}
}
