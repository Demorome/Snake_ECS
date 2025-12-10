using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Data;

public enum PrefabTypes
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
public readonly record struct PrefabID(PrefabTypes ID);

public static class PrefabsFuncs
{
    public static bool IsTile(PrefabTypes t)
    {
        return t == PrefabTypes.RegularSolidTile || t == PrefabTypes.VisualTile;
    }
    public static bool IsPurelyVisual(PrefabTypes t)
    {
        return t == PrefabTypes.Image || t == PrefabTypes.VisualTile;
    }
}

/// <summary>
/// Some prefabs need args to be spawned.
/// <para>See also: <see cref="FiledEntity.SpawnInfo"/> for serialized version.</para>
/// </summary>
public struct PrefabSpawnInfo
{
    public VisualFromSetID_ForSpawning? VisualFromSetID;
    //public SpriteAnimation? SpriteAnim; 

    public static PrefabSpawnInfo ForVisualFromSet(VisualFromSetID_ForSpawning visualFromSetID)
    {
        return new PrefabSpawnInfo{VisualFromSetID = visualFromSetID};
    }
}

// For unique changes to entities, like changing its color blend.
// See also: FiledEntity.ExtraSpawnInfo for serialized version.
public struct PrefabExtraSpawnInfo
{
    public Color? ColorBlendOverride;
    public Angle? AngleOverride;

    public static PrefabExtraSpawnInfo? FromFiled(FiledEntity.ExtraSpawnInfo? maybeFiledExtraSpawnInfo)
    {
        if (!maybeFiledExtraSpawnInfo.HasValue)
        {
            return null;
        }
        var filedExtraSpawnInfo = maybeFiledExtraSpawnInfo.Value;
        var result = new PrefabExtraSpawnInfo();

        if (filedExtraSpawnInfo.ColorBlendOverride.HasValue)
        {
            // FIXME: BitCast shouldn't be used here!! (endianness)
            /*result.ColorBlendOverride = Unsafe.BitCast<uint, Color>(
                filedExtraSpawnInfo.ColorBlendOverride.Value
            );*/
        }

        if (filedExtraSpawnInfo.AngleOverride.HasValue)
        {
            result.AngleOverride = new Angle(
                float.DegreesToRadians(filedExtraSpawnInfo.AngleOverride.Value)
            );
        }

        return result;
    }
}