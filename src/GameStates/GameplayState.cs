using System;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
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
    TargetingDirection TargetingDirection;
    FollowingSystem FollowingSystem;
    ChangeAppearanceOverTime ChangeAppearanceOverTime;
    DetectionSystem DetectionSystem;
    EnemySystem EnemySystem;
    TrailVisualSystem TrailVisualSystem;

    public GameplayState(RollAndCashGame game, GameState transitionState)
    {
        Game = game;
        TransitionState = transitionState;
    }

    const string LevelBoundsTag = "Level Bounds";
    const string StaticColliderTag = "Static";

    void CreateGameDimensionBorderCollision()
    {
        var topBorder = World.CreateEntity(LevelBoundsTag);
        World.Set(topBorder, new Position2D(0, 0));
        World.Set(topBorder, new Rectangle(0, 0, Dimensions.GAME_W, 10));
        World.Set(topBorder, new Layer(CollisionLayer.LevelCollider_ExistsOn, CollisionLayer.StaticLevelCollider_CollidesWith));

        var leftBorder = World.CreateEntity(LevelBoundsTag);
        World.Set(leftBorder, new Position2D(0, 0));
        World.Set(leftBorder, new Rectangle(0, 0, 10, Dimensions.GAME_H));
        World.Set(leftBorder, new Layer(CollisionLayer.LevelCollider_ExistsOn, CollisionLayer.StaticLevelCollider_CollidesWith));

        var rightBorder = World.CreateEntity(LevelBoundsTag);
        World.Set(rightBorder, new Position2D(Dimensions.GAME_W, 0));
        World.Set(rightBorder, new Rectangle(0, 0, 10, Dimensions.GAME_H));
        World.Set(rightBorder, new Layer(CollisionLayer.LevelCollider_ExistsOn, CollisionLayer.StaticLevelCollider_CollidesWith));

        var bottomBorder = World.CreateEntity(LevelBoundsTag);
        World.Set(bottomBorder, new Position2D(0, Dimensions.GAME_H));
        World.Set(bottomBorder, new Rectangle(0, 0, Dimensions.GAME_W, 10));
        World.Set(bottomBorder, new Layer(CollisionLayer.LevelCollider_ExistsOn, CollisionLayer.StaticLevelCollider_CollidesWith));
    }

    void CreateBattleAreaBorder()
    {
        const int x_offset = Dimensions.BATTLE_AREA_W / 2;
        const int y_offset = Dimensions.BATTLE_AREA_H / 2;
        const int thickness = Dimensions.BATTLE_AREA_THICKNESS;

        var topBorder = World.CreateEntity(StaticColliderTag);
        World.Set(topBorder, new Position2D(x_offset + thickness, y_offset));
        World.Set(topBorder, new Rectangle(0, 0, Dimensions.BATTLE_AREA_W - thickness, thickness));
        World.Set(topBorder, new Layer(CollisionLayer.LevelCollider_ExistsOn, CollisionLayer.StaticLevelCollider_CollidesWith));
        World.Set(topBorder, new DrawAsRectangle());

        var bottomBorder = World.CreateEntity(StaticColliderTag);
        World.Set(bottomBorder, new Position2D(x_offset + thickness, y_offset + Dimensions.BATTLE_AREA_H));
        World.Set(bottomBorder, new Rectangle(0, 0, Dimensions.BATTLE_AREA_W - thickness, thickness));
        World.Set(bottomBorder, new Layer(CollisionLayer.LevelCollider_ExistsOn, CollisionLayer.StaticLevelCollider_CollidesWith));
        World.Set(bottomBorder, new DrawAsRectangle());

        var leftBorder = World.CreateEntity(StaticColliderTag);
        World.Set(leftBorder, new Position2D(x_offset, y_offset));
        World.Set(leftBorder, new Rectangle(0, 0, thickness, Dimensions.BATTLE_AREA_H + thickness));
        World.Set(leftBorder, new Layer(CollisionLayer.LevelCollider_ExistsOn, CollisionLayer.StaticLevelCollider_CollidesWith));
        World.Set(leftBorder, new DrawAsRectangle());

        var rightBorder = World.CreateEntity(StaticColliderTag);
        World.Set(rightBorder, new Position2D(x_offset + Dimensions.BATTLE_AREA_W, y_offset));
        World.Set(rightBorder, new Rectangle(0, 0, thickness, Dimensions.BATTLE_AREA_H + thickness));
        World.Set(rightBorder, new Layer(CollisionLayer.LevelCollider_ExistsOn, CollisionLayer.StaticLevelCollider_CollidesWith));
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
        TargetingDirection = new TargetingDirection(World);
        FollowingSystem = new FollowingSystem(World);
        ChangeAppearanceOverTime = new ChangeAppearanceOverTime(World);
        DetectionSystem = new DetectionSystem(World);
        EnemySystem = new(World);
        TrailVisualSystem = new(World);

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
        ChangeAppearanceOverTime.Update(dt);
        UpdateSpriteAnimationSystem.Update(dt);
        Input.Update(dt);
        PlayerController.Update(dt);
        EnemySystem.Update(dt);
        DetectionSystem.Update(dt);
        Projectile.Update(dt);
        TargetingDirection.Update(dt);
        Motion.Update(dt);
        Collision.Update(dt);
        Health.Update(dt);
        TrailVisualSystem.Update(dt);
        FollowingSystem.Update(dt);
        DirectionalAnimation.Update(dt);
        SetSpriteAnimationSystem.Update(dt);
        ColorAnimation.Update(dt);
        FlickerSystem.Update(dt);
        FlipAnimationSystem.Update(dt);
        Audio.Update(dt);
        Destroyer.Update(dt);

#if DEBUG
        ImGuiHandler.DrawHelpWindow(World);
        ImGuiHandler.HandleDebugKeybinds(World);
        ImGuiHandler.DrawEntitiesWithComponentWindows(World);
        ImGuiHandler.DrawDetachedWindows(World);
#endif

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

    public override void Draw(CommandBuffer commandBuffer, Texture swapchainTexture, Window window, double alpha)
    {
        Renderer.Render(commandBuffer, swapchainTexture, window, alpha);
    }

    public override void End()
    {

    }
}
