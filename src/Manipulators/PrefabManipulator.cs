using System;
using System.Diagnostics;
using Hexa.NET.ImGui;
using MoonTools.ECS;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Systems;
using RollAndCash.Utility;
using RollAndCash;
using System.Collections.Generic;
using RollAndCash.Data;
using MoonWorks;
using System.Runtime.CompilerServices;






#if DEBUG
using RollAndCash.Editor;
#endif

public enum Prefabs
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
public readonly record struct PrefabID(Prefabs ID);

// Some prefabs need args to be spawned.
// See also: FiledEntity.SpawnInfo for serialized version.
public struct PrefabSpawnInfo
{
    public TileID? TileID;
    //public SpriteAnimation? SpriteAnim; 

    public static PrefabSpawnInfo ForTile(TileID tileID)
    {
        return new PrefabSpawnInfo{TileID = tileID};
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
            result.ColorBlendOverride = Unsafe.BitCast<uint, Color>(
                filedExtraSpawnInfo.ColorBlendOverride.Value
            );
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

public class PrefabManipulator : MoonTools.ECS.Manipulator
{
    MirrorManipulator MirrorManipulator;
    ActorManipulator PlayerManipulator;
    LevelObjectManipulator LevelObjectManipulator;
    TileManipulator TileManipulator;

    public PrefabManipulator(World world) : base(world)
    {
        MirrorManipulator = new(world);
        PlayerManipulator = new(world);
        LevelObjectManipulator = new(world);
        TileManipulator = new(world);
    }

    public Entity? TrySpawnPrefab(
        Prefabs prefabType,
        Position2D pos,
        bool rememberCreationForUndo = true,
        PrefabSpawnInfo? maybeSpawnInfo = null,
        FiledEntity.Flags spawnFlags = FiledEntity.Flags.None,
        PrefabExtraSpawnInfo? maybeExtraSpawnInfo = null
        )
    {
        Entity result;
        switch (prefabType)
        {
            case Prefabs.StaticLevelMirror:
                result = MirrorManipulator.CreateStaticLevelMirror(pos);
                break;
            case Prefabs.FrogEnemy:
                result = PlayerManipulator.SpawnFrog(pos);
                break;
            case Prefabs.InvisibleSolidRectangle:
                result = LevelObjectManipulator.SpawnInvisibleSolidRectangle(pos);
                break;
            case Prefabs.SolidRectangle:
                result = LevelObjectManipulator.SpawnSolidRectangle(pos, Color.White);
                break;
            case Prefabs.Player:
                result = PlayerManipulator.SpawnPlayer(pos, 0);
                break;
            case Prefabs.RegularSolidTile:
                if (!maybeSpawnInfo.HasValue || !maybeSpawnInfo.Value.TileID.HasValue)
                {
                    goto default;
                }
                result = TileManipulator.SpawnRegularSolidTile(pos, maybeSpawnInfo.Value.TileID.Value);
                break;
            case Prefabs.VisualTile:
                if (!maybeSpawnInfo.HasValue || !maybeSpawnInfo.Value.TileID.HasValue)
                {
                    goto default;
                }
                result = TileManipulator.SpawnVisualTile(pos, maybeSpawnInfo.Value.TileID.Value);
                break;
            /*case Prefabs.Image:
                if (maybeExtraSpawnInfo == null || !maybeExtraSpawnInfo.ContainsKey(FiledEntity.ExtraSpawnInfo.SpriteAnim))
                {
                    goto default;
                }
                result = CreateEntity();
                Set(result, pos);
                Set(result, (SpriteAnimation)maybeExtraSpawnInfo[FiledEntity.ExtraSpawnInfo.SpriteAnim]);
                break;*/

            default:
                Logger.LogError($"Failed to spawn prefab {prefabType}!");
                return null;
        }

#if DEBUG
        if (GetTag(result).Length == 0)
        {
            Tag(result, prefabType.ToString());
        }
        
        if (!Has<PrefabID>(result))
        {
            //Logger.LogError($"Spawned prefab {prefabType} should have received a prefabID!");
            Set(result, new PrefabID(prefabType));
        }
#endif

        if (maybeExtraSpawnInfo.HasValue)
        {
            /*if (ExtraSpawnInfo.ContainsKey(FiledEntity.ExtraDataTypes.SpriteAnimOverride))
            {
                Set(result, (SpriteAnimation)ExtraSpawnInfo[FiledEntity.ExtraDataTypes.SpriteAnimOverride]);
            }*/
            if (maybeExtraSpawnInfo.Value.ColorBlendOverride.HasValue)
            {
                Set(result, new ColorBlend(maybeExtraSpawnInfo.Value.ColorBlendOverride.Value));
            }
            if (maybeExtraSpawnInfo.Value.AngleOverride.HasValue)
            {
                Set(result, maybeExtraSpawnInfo.Value.AngleOverride.Value);
            }
        }

        if (rememberCreationForUndo)
        {
            UndoRedo.RememberEntityCreation(result, World);
        }

        return result;
    }

#if DEBUG

    public bool IsDefaultSprite(SpriteAnimation spriteToCheck, Prefabs prefabType)
    {
        bool result;
        var dummyPrefab = TrySpawnPrefab(prefabType, Input.WorldMousePosition).Value;
        if (!Has<SpriteAnimation>(dummyPrefab))
        {
            result = false;
        }
        else
        {
            result = Get<SpriteAnimation>(dummyPrefab).SpriteAnimationInfoID == spriteToCheck.SpriteAnimationInfoID;
        }
        Destroy(dummyPrefab);
        return result;
    }
    public bool IsDefaultColorBlend(Color colorBlend, Prefabs prefabType)
    {
        bool result;
        var dummyPrefab = TrySpawnPrefab(prefabType, Input.WorldMousePosition).Value;
        if (!Has<ColorBlend>(dummyPrefab))
        {
            result = false;
        }
        else
        {
            result = Get<ColorBlend>(dummyPrefab).Color == colorBlend;
        }
        Destroy(dummyPrefab);
        return result;
    }

    private Prefabs PrefabToSpawn_ForPreview = Prefabs.None;

    private void SetUpSelectedPrefabPreviewVisuals(Entity debugEntity)
    {
        if (!ImGui.GetIO().WantCaptureMouse)
        {
            // Spawn a copy of the prefab, then extract its visual info.
            var dummyPrefab = TrySpawnPrefab(PrefabToSpawn_ForPreview, Input.WorldMousePosition, true).Value;

            Set(debugEntity, Get<Position2D>(dummyPrefab));
            if (Has<SpriteScale>(dummyPrefab))
            {
                Set(debugEntity, Get<SpriteScale>(dummyPrefab));
            }
            if (Has<SpriteAnimation>(dummyPrefab))
            {
                Set(debugEntity, Get<SpriteAnimation>(dummyPrefab));
            }
            if (Has<DrawAsRectangle>(dummyPrefab))
            {
                Set(debugEntity, Get<DrawAsRectangle>(dummyPrefab));
                Set(debugEntity, Get<Rectangle>(dummyPrefab));
            }
            if (Has<ColorBlend>(dummyPrefab))
            {
                Set(debugEntity, new ColorBlend(
                    Color.Lerp(Get<ColorBlend>(dummyPrefab).Color, Color.Transparent, 0.25f)
                    )
                );
            }
            else
            {
                Set(debugEntity, new ColorBlend(new Color(255, 255, 255, 191)));
            }
            if (Has<Angle>(dummyPrefab))
            {
                Set(debugEntity, Get<Angle>(dummyPrefab));
            }
            if (Has<RotatesWithDirection>(dummyPrefab))
            {
                Set(debugEntity, new Angle(
                    MathUtilities.AngleFromUnitVector(
                        Get<Direction2D>(dummyPrefab).Value)
                    )
                );
            }
            if (Has<Depth>(dummyPrefab))
            {
                Set(debugEntity, Get<Depth>(dummyPrefab));
            }

            Destroy(dummyPrefab);
        }
        else
        {
            // Remove visual info.
            Remove<Position2D>(debugEntity);
            Remove<SpriteScale>(debugEntity);
            Remove<SpriteAnimation>(debugEntity);
            Remove<ColorBlend>(debugEntity);
            Remove<Angle>(debugEntity);
            Remove<DrawAsRectangle>(debugEntity);
            Remove<Rectangle>(debugEntity);
            Remove<Depth>(debugEntity);
        }
    }

    // Returns if a selection is made or not.
    public bool ShowPrefabSpawner(Entity debugEntity)
    {
        if (ImGui.Begin("Prefab Objects"u8))
        {
            foreach (Prefabs prefab in Enum.GetValues(typeof(Prefabs)))
            {
                if (prefab == Prefabs.None)
                {
                    continue;
                }
                if (prefab >= Prefabs.SPAWNED_NORMALLY_COUNT)
                {
                    continue;
                }

                bool isSelected = PrefabToSpawn_ForPreview == prefab;
                ImGui.PushStyleColor(ImGuiCol.Header, Color.Green.ToVector4());
                if (ImGui.Selectable(prefab.ToString(), isSelected))
                {
                    PrefabToSpawn_ForPreview = isSelected ? Prefabs.None : prefab;
                }
                ImGui.PopStyleColor();
            }

            // TODO: Once button to spawn a prefab entity is pressed, make it appear transparent below cursor.
            if (PrefabToSpawn_ForPreview != Prefabs.None)
            {
                if (!ImGui.GetIO().WantCaptureMouse
                    && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    TrySpawnPrefab(PrefabToSpawn_ForPreview, Input.WorldMousePosition);
                }
            }
        }
        ImGui.End();

        if (PrefabToSpawn_ForPreview != Prefabs.None)
        {
            SetUpSelectedPrefabPreviewVisuals(debugEntity);
            return true;
        }
        return false;
    }
#endif
}