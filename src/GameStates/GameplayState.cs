using System;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
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
    NPCController NPCController;
    PlayerController PlayerController;
    Growth Growth;
    FoodSpawner FoodSpawner;
    AssignTilePixelPositions AssignTilePixelPositions;
    GameState TransitionState;

    TileGrid TileGrid;

    public GameplayState(SnakeGame game, GameState transitionState)
    {
        Game = game;
        TransitionState = transitionState;
    }

    public override void Start()
    {
        World = new World();

        TileGrid = new TileGrid(World);

        //GameTimer = new(World);
        //Timing = new(World);
        Input = new Input(World, Game.Inputs);
        Motion = new Motion(World, TileGrid);
        Audio = new Audio(World, Game.AudioDevice);
        PlayerController = new PlayerController(World);
        Growth = new Growth(World);
        FoodSpawner = new FoodSpawner(World, TileGrid);
        AssignTilePixelPositions = new AssignTilePixelPositions(World, TileGrid);
        SetSpriteAnimationSystem = new SetSpriteAnimationSystem(World);
        UpdateSpriteAnimationSystem = new UpdateSpriteAnimationSystem(World);
        ColorAnimation = new ColorAnimation(World);
        DirectionalAnimation = new DirectionalAnimation(World);
        NPCController = new NPCController(World, TileGrid);

        Renderer = new Renderer(World, Game.GraphicsDevice, Game.RootTitleStorage, Game.MainWindow.SwapchainFormat);

        for (int i = 0; i < GridInfo.WidthWithWalls; i++)
        {
            for (int j = 0; j < GridInfo.HeightWithWalls; j++)
            {
                #region Create walls
                if (i == 0 || j == 0 || (i == (GridInfo.WidthWithWalls-1)) || (j == (GridInfo.HeightWithWalls-1)))
                {
                   var wall = World.CreateEntity();
                   var newPos = new Vector2(i, j);
                   World.Set(wall, new TilePosition(newPos));
                   World.Set(wall, new Rectangle(0, 0, GridInfo.PixelCellSize, GridInfo.PixelCellSize));
                   World.Set(wall, new DrawAsRectangle());
                   World.Set(wall, new Solid());
                   //Motion.UpdateTilePosition(wall, newPos);
                }
                #endregion

                //#region Grid cell visuals
                //#endregion
            }
        }

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
        World.Send(new GrowPlayer(playerOne, 6));

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
        NPCController.Update(dt);
        Motion.Update(dt);
        Growth.Update(dt);
        FoodSpawner.Update(dt);
        AssignTilePixelPositions.Update(dt);
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
