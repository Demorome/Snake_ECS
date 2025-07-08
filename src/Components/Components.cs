using MoonWorks.Graphics;
using RollAndCash.Systems;
using RollAndCash.Data;
using RollAndCash.Messages;
using System.Numerics;

namespace RollAndCash.Components;

public readonly record struct Rectangle(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Right => X + Width;
    public int Top => Y;
    public int Bottom => Y + Height;

    public bool Intersects(Rectangle other)
    {
        return
            other.Left < Right &&
            Left < other.Right &&
            other.Top < Bottom &&
            Top < other.Bottom;
    }

    public static Rectangle Union(Rectangle a, Rectangle b)
    {
        var x = int.Min(a.X, a.X);
        var y = int.Min(a.Y, b.Y);
        return new Rectangle(
            x,
            y,
            int.Max(a.Right, b.Right) - x,
            int.Max(a.Bottom, b.Bottom) - y
        );
    }

    public Rectangle Inflate(int horizontal, int vertical)
    {
        return new Rectangle(
            X - horizontal,
            Y - vertical,
            Width + horizontal * 2,
            Height + vertical * 2
        );
    }

    public Rectangle GetWorldRect(Position2D p)
    {
        return new Rectangle(p.X + X, p.Y + Y, Width, Height);
    }

    public Rectangle GetWorldRect(Vector2 p)
    {
        return new Rectangle((int)(p.X + X), (int)(p.Y + Y), Width, Height);
    }

    public Vector2 TopLeft()
    {
        return new Vector2(Left, Top);
    }
    public Vector2 BottomRight()
    {
        return new Vector2(Right, Bottom);
    }
    public Vector2 BottomLeft()
    {
        return new Vector2(Left, Bottom);
    }
    public Vector2 TopRight()
    {
        return new Vector2(Right, Top);
    }
}

public readonly record struct Player(int Index);
public readonly record struct Angle(float Value);
//public readonly record struct Solid();
public readonly record struct TouchingSolid();
public readonly record struct Name(int TextID);

public readonly record struct Score(int Value);
public readonly record struct DisplayScore(int Value);
public readonly record struct ColorBlend(Color Color);
public readonly record struct Alpha(byte Value); // 0-255, overrides the alpha in ColorBlend

public readonly record struct ColorSpeed(float RedSpeed, float GreenSpeed, float BlueSpeed);

public readonly record struct Depth(float Value); // Deeper depth = higher value.
public readonly record struct DrawAsRectangle();

public readonly record struct TextDropShadow(int OffsetX, int OffsetY);
public readonly record struct ForceIntegerMovement();
public readonly record struct MaxSpeed(float Value);
public readonly record struct Speed(float Value);
public readonly record struct SpeedAcceleration(float Value);
public readonly record struct AdjustFramerateToSpeed();

public readonly record struct Direction2D(System.Numerics.Vector2 Value);
public readonly record struct SlowDownAnimation(int BaseSpeed, int step);

//public readonly record struct IsPopupBox(); // jank because we cant check relation type count
//public readonly record struct CanSpawn(int Width, int Height);
public readonly record struct FallSpeed(float Speed);
public readonly record struct DestroyAtScreenBottom();
public readonly record struct GameInProgress(); // yaaargh

public readonly record struct DirectionalSprites(
    SpriteAnimationInfoID Up,
    SpriteAnimationInfoID UpRight,
    SpriteAnimationInfoID Right,
    SpriteAnimationInfoID DownRight,
    SpriteAnimationInfoID Down,
    SpriteAnimationInfoID DownLeft,
    SpriteAnimationInfoID Left,
    SpriteAnimationInfoID UpLeft
    );

public readonly record struct AccelerateToPosition(Position2D Target, float Acceleration, float MotionDampFactor);
public readonly record struct DestroyAtGameEnd();
public readonly record struct DealsDamageOnContact(int Damage);
public readonly record struct CanDetect(float ConeRadius, float MaxDistance);
public readonly record struct DrawDetectionCone();
public readonly record struct CanBeDetected();
public readonly record struct ChargingUpAttack();


public readonly record struct HasVisualTrail();

public readonly record struct DestroyWhenOutOfBounds();
public readonly record struct DestroyForDebugTestReasons();
public readonly record struct ColorFlicker(int ElapsedFrames, Color Color);
public readonly record struct MotionDamp(float Damping);
public readonly record struct SpriteScale(System.Numerics.Vector2 Scale);
public readonly record struct LastValue(int value);
public readonly record struct PlaySoundOnTimerEnd(PlayStaticSoundMessage PlayStaticSoundMessage);

public readonly record struct UpdateDirectionToTargetPosition(bool DoOnce);
public readonly record struct DestroyWhenNoSource();
public readonly record struct DestroyWhenNoTarget();

public readonly record struct HitscanSpeed(float Value);
public readonly record struct TargetPosition(Vector2 Value);


// Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
[System.Flags]
public enum CollisionLayer
{
    None = 0,
    Level = 1,
    Actor = 2,
    Player = 4,
    Enemy = 8,
    Bullet = 16,
    Pickup = 32,

    LevelCollider_ExistsOn = Level,
    StaticLevelCollider_CollidesWith = None, // a static level setpiece doesn't need to do collision, since it won't move.

    PlayerActor_ExistsOn = Player | Actor,
    PlayerActor_CollidesWith = Actor | Bullet | Pickup,

    EnemyActor_ExistsOn = Enemy | Actor,
    EnemyActor_CollidesWith = Actor,

    PlayerBullet_ExistsOn = Bullet,
    PlayerBullet_CollidesWith = Enemy | Level,

    EnemyBullet_ExistsOn = Bullet,
    EnemyBullet_CollidesWith = Player | Level,

    DetectionCone_ExistsOn = None,
    DetectionCone_CollidesWith = Player | Level
}
public readonly record struct Layer(CollisionLayer ExistsOn, CollisionLayer CollideWith);
public readonly record struct CanMoveThroughDespiteCollision(CollisionLayer Value);

public readonly record struct BecomeInvincibleOnDamage(float Time);
public readonly record struct MarkedForDestroy();
public readonly record struct DestroyOnImpact();
public readonly record struct HasHealth(int Health);

// FIXME: Implement behavior
public readonly record struct MaxMovementDistance(float Value);

public readonly record struct CursorPosition(Vector2 Value);
