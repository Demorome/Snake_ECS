using System;
using MoonTools.ECS;
using MoonWorks;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Systems;

namespace RollAndCash.GameStates;

public class GameplayState : GameState
{
    RollAndCashGame Game;

    Renderer Renderer;
    World World;
    Input Input;
    Motion Motion;
    Audio Audio;
    Timing Timing;
    SetSpriteAnimationSystem SetSpriteAnimationSystem;
    DirectionalAnimation DirectionalAnimation;
    UpdateSpriteAnimationSystem UpdateSpriteAnimationSystem;
    ColorAnimation ColorAnimation;
    PlayerController PlayerController;
    GameState TransitionState;
    Health Health;
    Projectile Projectile;
    Collision Collision;
    Destroyer Destroyer;
    FlickerSystem FlickerSystem;
    FlipAnimationSystem FlipAnimationSystem;

    public GameplayState(RollAndCashGame game, GameState transitionState)
    {
        Game = game;
        TransitionState = transitionState;
    }

    void CreateGameDimensionBorderCollision()
    {
        var topBorder = World.CreateEntity();
        World.Set(topBorder, new Position(0, 0));
        World.Set(topBorder, new Rectangle(0, 0, Dimensions.GAME_W, 10));
        World.Set(topBorder, new Layer(CollisionLayer.Level));

        var leftBorder = World.CreateEntity();
        World.Set(leftBorder, new Position(0, 0));
        World.Set(leftBorder, new Rectangle(0, 0, 10, Dimensions.GAME_H));
        World.Set(leftBorder, new Layer(CollisionLayer.Level));

        var rightBorder = World.CreateEntity();
        World.Set(rightBorder, new Position(Dimensions.GAME_W, 0));
        World.Set(rightBorder, new Rectangle(0, 0, 10, Dimensions.GAME_H));
        World.Set(rightBorder, new Layer(CollisionLayer.Level));

        var bottomBorder = World.CreateEntity();
        World.Set(bottomBorder, new Position(0, Dimensions.GAME_H));
        World.Set(bottomBorder, new Rectangle(0, 0, Dimensions.GAME_W, 10));
        World.Set(bottomBorder, new Layer(CollisionLayer.Level));
    }

    void CreateBattleAreaBorder()
    {
        const int x_offset = Dimensions.BATTLE_AREA_W / 2;
        const int y_offset = Dimensions.BATTLE_AREA_H / 2;
        const int thickness = Dimensions.BATTLE_AREA_THICKNESS;

        var topBorder = World.CreateEntity();
        World.Set(topBorder, new Position(x_offset + thickness, y_offset));
        World.Set(topBorder, new Rectangle(0, 0, Dimensions.BATTLE_AREA_W - thickness, thickness));
        World.Set(topBorder, new Layer(CollisionLayer.Level));
        World.Set(topBorder, new DrawAsRectangle());

        var bottomBorder = World.CreateEntity();
        World.Set(bottomBorder, new Position(x_offset + thickness, y_offset + Dimensions.BATTLE_AREA_H));
        World.Set(bottomBorder, new Rectangle(0, 0, Dimensions.BATTLE_AREA_W - thickness, thickness));
        World.Set(bottomBorder, new Layer(CollisionLayer.Level));
        World.Set(bottomBorder, new DrawAsRectangle());

        var leftBorder = World.CreateEntity();
        World.Set(leftBorder, new Position(x_offset, y_offset));
        World.Set(leftBorder, new Rectangle(0, 0, thickness, Dimensions.BATTLE_AREA_H + thickness));
        World.Set(leftBorder, new Layer(CollisionLayer.Level));
        World.Set(leftBorder, new DrawAsRectangle());

        var rightBorder = World.CreateEntity();
        World.Set(rightBorder, new Position(x_offset + Dimensions.BATTLE_AREA_W, y_offset));
        World.Set(rightBorder, new Rectangle(0, 0, thickness, Dimensions.BATTLE_AREA_H + thickness));
        World.Set(rightBorder, new Layer(CollisionLayer.Level));
        World.Set(rightBorder, new DrawAsRectangle());
    }

    public override void Start()
    {
        World = new World();

        Timing = new(World);
        Input = new Input(World, Game.Inputs);
        Motion = new Motion(World);
        Audio = new Audio(World, Game.AudioDevice);
        PlayerController = new PlayerController(World);
        SetSpriteAnimationSystem = new SetSpriteAnimationSystem(World);
        UpdateSpriteAnimationSystem = new UpdateSpriteAnimationSystem(World);
        ColorAnimation = new ColorAnimation(World);
        DirectionalAnimation = new DirectionalAnimation(World);
        Health = new Health(World);
        Projectile = new Projectile(World);
        Collision = new Collision(World);
        Destroyer = new Destroyer(World);
        FlickerSystem = new FlickerSystem(World);
        FlipAnimationSystem = new FlipAnimationSystem(World);

        Renderer = new Renderer(World, Game.GraphicsDevice, Game.RootTitleStorage, Game.MainWindow.SwapchainFormat);

        CreateGameDimensionBorderCollision();
        CreateBattleAreaBorder();

        /*
        var background = World.CreateEntity();
        World.Set(background, new Position(0, 0));
        World.Set(background, new Depth(999));
        World.Set(background, new SpriteAnimation(Content.SpriteAnimations.BG, 0));
        */

        /*
        var timer = World.CreateEntity();
        World.Set(timer, new Position(Dimensions.GAME_W * 0.5f, 38));
        World.Set(timer, new TextDropShadow(1, 1));
        */

        /*
        var scoreOne = World.CreateEntity();
        World.Set(scoreOne, new Position(80, 345));
        World.Set(scoreOne, new Score(0));
        World.Set(scoreOne, new DisplayScore(0));
        World.Set(scoreOne, new Text(Fonts.KosugiID, FontSizes.SCORE, "0"));
        */

        var playerOne = PlayerController.SpawnPlayer(0);

        var gameInProgressEntity = World.CreateEntity();
        World.Set(gameInProgressEntity, new GameInProgress());

        World.Send(new PlaySongMessage());

    }

    public override void Update(TimeSpan dt)
    {
        Timing.Update(dt);
        UpdateSpriteAnimationSystem.Update(dt);
        Input.Update(dt);
        PlayerController.Update(dt);
        Projectile.Update(dt);
        Motion.Update(dt);
        Collision.Update(dt);
        Health.Update(dt);
        DirectionalAnimation.Update(dt);
        SetSpriteAnimationSystem.Update(dt);
        ColorAnimation.Update(dt);
        FlickerSystem.Update(dt);
        FlipAnimationSystem.Update(dt);
        Audio.Update(dt);
        Destroyer.Update(dt);

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
