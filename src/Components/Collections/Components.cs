using MoonWorks.Graphics;
using RollAndCash.Systems;
using RollAndCash.Data;
using RollAndCash.Messages;
using System.Numerics;
using System;
using System.Text.Json.Serialization;
using RollAndCash.ComponentSerialization;

namespace RollAndCash.Components;

public readonly record struct Rectangle(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Right => X + Width;
    public int Top => Y;
    public int Bottom => Y + Height;
    public Vector2 Size => new Vector2(Width, Height);
    public Vector2 Center => new Vector2(X + (Width / 2), Y + (Height / 2));

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

public readonly record struct LastPosition(Position2D Value);
public readonly record struct Player(int Index);

/// <summary>
/// Prefer working in Radians, since most math functions use those.
/// </summary>
[JsonConverter(typeof(AngleJsonConverter))]
public readonly record struct Angle()
{
    public readonly float ValueInRadians;
    public readonly float ValueInDegrees => float.RadiansToDegrees(ValueInRadians);

    // Private, to enforce use of the explicit static constructors.
    private Angle(float valueInRadians) : this()
    {
        ValueInRadians = valueInRadians;
    }

    // Type-safe explicit static constructors.
    public static Angle FromDegrees(float valueInDegrees)
    {
        return new Angle(float.DegreesToRadians(valueInDegrees));
    }
    public static Angle FromRadians(float valueInRadians)
    {
        return new Angle(valueInRadians);
    }
};

public readonly record struct RotatesWithDirection(); // No need to touch Angle at all with this

//public readonly record struct Solid();
public readonly record struct TouchingSolid();
public readonly record struct Name(int TextID);

public readonly record struct Score(int Value);
public readonly record struct DisplayScore(int Value);

/// <summary>
/// Applies a tint to an entire sprite.
/// </summary>
/// <param name="Color"></param>
public readonly record struct ColorBlend()
{
    public readonly Color Color;

#if DEBUG
    /// <summary>
    /// The original base color for the entity.
    /// We need to store it, since the above Color is the final result
    /// of a mix between the layer's color and the entity's base color.
    /// Thus, when we switch the layer's color in the editor, we need to
    /// be able to refer back to the original color.
    /// </summary>
    public readonly Color? Editor_MaybeBaseColor;

    public ColorBlend(Color finalColorBlend, Color? maybeBaseColor)
        : this(finalColorBlend)
    {
        Editor_MaybeBaseColor = maybeBaseColor;
    }
#endif

    /// <summary>
    /// For default constructor, assume that we're setting the base color.
    /// </summary>
    public ColorBlend(Color finalColorBlend) : this()
    {
        Color = finalColorBlend;
#if DEBUG
        Editor_MaybeBaseColor = finalColorBlend;
#endif
    }
}

/// <summary>
/// 0-255, overrides the alpha in ColorBlend when rendering.
/// </summary>
public readonly record struct AlphaOverride(byte Value); 
public readonly record struct HorizontalFlip();
public readonly record struct VerticalFlip();
public readonly record struct ColorSpeed(float RedSpeed, float GreenSpeed, float BlueSpeed);

/// <summary>
/// Deeper depth = higher positive value = gets drawn below others.
/// We'll inverse it automatically if needed for rendering.
/// <para>NOTE: Currently, FarPlane is set to 1000 for rendering. 
/// Near plane is 0.01.
/// TODO: Should probably directly work with those values as limits here.</para>
/// </summary>
public enum DepthLayer
{
    //== Foreground
    PlaceholderDepth = 0, // draw above all, even UIs, to be obnoxious.
    GameUI_Lowest = 2, // could be 1, but I'm leaving space for ImGui UI, should it need a Depth value.
    GameUI_Highest = 15,
#if DEBUG
    Editor_SelectionOutline = GameUI_Highest + 1, // Render above everything (except menus).
    Debug_CollisionVisual = Editor_SelectionOutline,
    Debug_GridVisual = Debug_CollisionVisual + 1,
#endif
    // WARNING: These values should be LOCKED IN, 
    // for editor-created foreground layers to not have to be updated.
    Foreground_Lowest = 20,
    Foreground_Highest = 30, // leave some wiggle room for custom foreground layers

    //== Game objects (middle-ground)
    // Don't really need to leave gaps, 
    // since these will never directly get a Depth assigned in-editor.
    Player = Foreground_Highest + 1, // draw below foreground, but above most objects.
    Enemy = Player + 1, // draw below player
    LowestActor = Enemy,
    SolidObject = LowestActor + 1, // draw below actors
    DetectionCone = SolidObject + 1, // draw above background tiles, but below solid objects.

    //== Background
    Background = 1000
}
public readonly record struct Depth(float Value)
{
    public Depth(DepthLayer layer) : this((float)layer) { }
} 

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
public readonly record struct DestroyOnTransition(); // Name from Samurai Gunn 2 code
public readonly record struct DealsDamageOnContact(int Damage);
public readonly record struct CanDetect(float ConeRadius, float MaxDistance);
public readonly record struct DrawDetectionCone();
public readonly record struct CanBeDetected();
public readonly record struct ChargingUpAttack();

public readonly record struct CameraFocus();

public readonly record struct HasVisualTrail();

public readonly record struct DestroyWhenOutOfBounds();
public readonly record struct DestroyForDebugTestReasons();
public readonly record struct ColorFlicker(int ElapsedFrames, Color Color);
public readonly record struct MotionDamp(float Damping);
public readonly record struct VisualScale(System.Numerics.Vector2 Scale);
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
    Projectile = 16,
    Pickup = 32,

    LevelCollider_ExistsOn = Level,
    StaticLevelCollider_CollidesWith = None, // a static level setpiece doesn't need to do collision, since it won't move.

    PlayerActor_ExistsOn = Player | Actor,
    PlayerActor_CollidesWith = Actor | Projectile | Pickup | Level,

    EnemyActor_ExistsOn = Enemy | Actor,
    EnemyActor_CollidesWith = Actor,

    PlayerBullet_ExistsOn = Projectile,
    PlayerBullet_CollidesWith = Enemy | Level,

    EnemyBullet_ExistsOn = Projectile,
    EnemyBullet_CollidesWith = Player | Level,

    DetectionCone_ExistsOn = None,
    DetectionCone_CollidesWith = Player | Level
}
public readonly record struct Layer(CollisionLayer ExistsOn, CollisionLayer CollideWith);
public readonly record struct CanMoveThroughDespiteCollision(CollisionLayer Value);

// A line hitbox can be angled, unlike an AABB hitbox.
// The entity will have a Rectangle (AABB) generated for it that encompasses its area, for the broad collision pass.
public readonly record struct HasLineHitbox();

 // If a Projectile-layer entity hits this, their direction is reflected.
public readonly record struct ReflectsProjectiles();

public readonly record struct BecomeInvincibleOnDamage(float Time);
public readonly record struct DestroyOnImpact();
public readonly record struct HasHealth(int Health);

/// <summary>
/// To delay entity's destruction to the end of the frame. <br/>
/// Useful if other entities should still be able to interact with it 
/// before it's destroyed on that frame.
/// </summary>
public readonly record struct MarkedForDestroy();

/// <summary>
/// To prevent an entity from being used in most systems. <br/>
/// Usually reserved for entities that are in a nearby loaded room, 
/// who should act frozen until the room is entered. <br/>
/// Note that this will also prevent rendering for that entity. <br/>
/// FIXME: We aren't checking for this whenever we loop over Relations!!!
/// </summary>
public readonly record struct Disabled();

public readonly record struct LevelStart(
    LevelRoomID StartRoomID,
    Position2D PlayerStartPosition
);

// FIXME: Implement behavior
public readonly record struct MaxMovementDistance(float Value);

public readonly record struct CursorPosition(Vector2 Value);


#if DEBUG
    public readonly record struct Editor_DummyVisualFromVisualSet_ForPaintingPreview();
    public readonly record struct Editor_DummyVisualFromVisualSet_ForVisualSet();
    public readonly record struct Editor_DontShowInLists();
    public readonly record struct Editor_DontAddToLevel();
#endif