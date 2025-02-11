using System;
using MoonTools.ECS;
using MoonWorks;
using Snake.Components;
using Snake.Content;
using Snake.Messages;
using Snake.Relations;
using Snake.Systems;

namespace Snake.GameStates;

public class GameplayState : GameState
{
    SnakeGame Game;

    Renderer Renderer;
    World World;
    Input Input;
    Motion Motion;
    Audio Audio;
    //Systems.GameTimer GameTimer;
    //Timing Timing;
    SetSpriteAnimationSystem SetSpriteAnimationSystem;
    DirectionalAnimation DirectionalAnimation;
    UpdateSpriteAnimationSystem UpdateSpriteAnimationSystem;
    ColorAnimation ColorAnimation;
    //NPCController NPCController;
    PlayerController PlayerController;
    GameState TransitionState;

    public GameplayState(SnakeGame game, GameState transitionState)
    {
        Game = game;
        TransitionState = transitionState;
    }

    public override void Start()
    {
        World = new World();

        //GameTimer = new(World);
        //Timing = new(World);
        Input = new Input(World, Game.Inputs);
        Motion = new Motion(World);
        Audio = new Audio(World, Game.AudioDevice);
        PlayerController = new PlayerController(World);
        SetSpriteAnimationSystem = new SetSpriteAnimationSystem(World);
        UpdateSpriteAnimationSystem = new UpdateSpriteAnimationSystem(World);
        ColorAnimation = new ColorAnimation(World);
        DirectionalAnimation = new DirectionalAnimation(World);
        //NPCController = new NPCController(World);

        Renderer = new Renderer(World, Game.GraphicsDevice, Game.RootTitleStorage, Game.MainWindow.SwapchainFormat);

        var topBorder = World.CreateEntity();
        World.Set(topBorder, new PixelPosition(0, 65));
        World.Set(topBorder, new Rectangle(0, 0, Dimensions.GAME_W, 10));
        World.Set(topBorder, new Solid());

        var leftBorder = World.CreateEntity();
        World.Set(leftBorder, new PixelPosition(-10, 0));
        World.Set(leftBorder, new Rectangle(0, 0, 10, Dimensions.GAME_H));
        World.Set(leftBorder, new Solid());

        var rightBorder = World.CreateEntity();
        World.Set(rightBorder, new PixelPosition(Dimensions.GAME_W, 0));
        World.Set(rightBorder, new Rectangle(0, 0, 10, Dimensions.GAME_H));
        World.Set(rightBorder, new Solid());

        var bottomBorder = World.CreateEntity();
        World.Set(bottomBorder, new PixelPosition(0, Dimensions.GAME_H));
        World.Set(bottomBorder, new Rectangle(0, 0, Dimensions.GAME_W, 10));
        World.Set(bottomBorder, new Solid());

        var background = World.CreateEntity();
        World.Set(background, new PixelPosition(0, 0));
        World.Set(background, new Depth(999));
        World.Set(background, new SpriteAnimation(Content.SpriteAnimations.BG, 0));

        /*
        var timer = World.CreateEntity();
        World.Set(timer, new Components.GameTimer(90));
        World.Set(timer, new Position(Dimensions.GAME_W * 0.5f, 38));
        World.Set(timer, new TextDropShadow(1, 1));*/

        var playerOne = PlayerController.SpawnPlayer(0);
        //var playerTwo = PlayerController.SpawnPlayer(1);

        //World.Relate(playerOne, scoreOne, new HasScore());
        //World.Relate(playerTwo, scoreTwo, new HasScore());

        var gameInProgressEntity = World.CreateEntity();
        World.Set(gameInProgressEntity, new GameInProgress());
        World.Send(new PlaySongMessage());
    }

    public override void Update(TimeSpan dt)
    {
        //Timing.Update(dt);
        UpdateSpriteAnimationSystem.Update(dt);
        //GameTimer.Update(dt);
        Input.Update(dt);
        PlayerController.Update(dt);
        //NPCController.Update(dt);
        Motion.Update(dt);
        DirectionalAnimation.Update(dt);
        SetSpriteAnimationSystem.Update(dt);
        ColorAnimation.Update(dt);
        Audio.Update(dt);

        if (World.SomeMessage<EndGame>())
        {
            World.FinishUpdate();
            Audio.Cleanup();
            World.Dispose();
            Game.SetState(TransitionState);
            return;
        }

        World.FinishUpdate();
    }

    public override void Draw(Window window, double alpha)
    {
        Renderer.Render(Game.MainWindow);
    }

    public override void End()
    {

    }
}
