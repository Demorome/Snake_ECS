using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Data;

public enum PrefabType
{
    None = 0,
    StaticLevelMirror,
    FrogEnemy,
    SolidRectangle,
    InvisibleSolidRectangle,
    Player,

    SPAWNED_NORMALLY_COUNT,

    //== These shouldn't be spawned manually, since they rely on vital info from another source anyways.
    // They are here solely to tag special entities for save/loading.
    RegularSolidTile,
    VisualTile,
    Image
}

/// <summary>
/// Should only be set on entities in editor/debug mode.
/// Tells us what prefab an entity is based on, 
/// so that we can save this entity with minimal information.
/// </summary>
public readonly record struct Editor_PrefabID(PrefabType ID);

public static partial class EntityPrefabs
{
    public static bool IsTile(PrefabType t)
    {
        return t == PrefabType.RegularSolidTile 
            || t == PrefabType.VisualTile;
    }
    public static bool IsPurelyVisual(PrefabType t)
    {
        return t == PrefabType.Image 
            || t == PrefabType.VisualTile;
    }
}

/// <summary>
/// Some prefabs need args to be spawned.
/// TODO: Damn you C# for not having Discriminated Unions yet!!! 
/// TODO: We'd use that + PrefabTypes union here.
/// </summary>
public struct PrefabSpawnInfo_Filed
{
    public PositionInVisualSet? PosInVisualSet;
    public SpriteAnimation? SpriteAnim; 
}

/// <summary>
/// Some filed entity info needs to be supplemented w/ info from 
/// parents, like a parent layer/level, so that we can spawn them.
/// <para>Value are ordered 1-1 to 
/// <see cref="PrefabSpawnInfo_Filed"/>.</para>
/// FIXME: Use discriminated union instead when available!
/// </summary>
public struct PrefabSpawnInfo_Processed
{
    public VisualFromSetID_ForSpawning? VisualFromSetID;
    public SpriteAnimation? SpriteAnim; 

    //== Constructors
    public PrefabSpawnInfo_Processed(VisualFromSetID_ForSpawning arg)
    {
        VisualFromSetID = arg;
    }
    public PrefabSpawnInfo_Processed(SpriteAnimation arg)
    {
        SpriteAnim = arg;
    }
}

/// <summary>
/// For unique changes to entities, like changing its color blend.
/// <para>These changes always apply last when the entity is created, 
/// thus the 'override'.</para>
/// <para>We manually pick what to save, since a deny-list 
/// for components would be hard to maintain.</para>
/// </summary>
public struct PrefabSpawnInfoOverride
{
    public ColorBlend? ColorBlend;
    public Angle? Angle;
}