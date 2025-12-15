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
        PrefabTypes prefabType,
        Position2D pos,
        bool rememberCreationForUndo = true,
        PrefabSpawnInfo_Processed spawnInfo = default,
        FiledEntity.Flags spawnFlags = FiledEntity.Flags.None,
        PrefabSpawnInfoOverride? maybeSpawnInfoOverrides = null
        )
    {
        Entity result;
        switch (prefabType)
        {
            case PrefabTypes.StaticLevelMirror:
                result = MirrorManipulator.CreateStaticLevelMirror(pos);
                break;
            case PrefabTypes.FrogEnemy:
                result = PlayerManipulator.SpawnFrog(pos);
                break;
            case PrefabTypes.InvisibleSolidRectangle:
                result = LevelObjectManipulator.SpawnInvisibleSolidRectangle(pos);
                break;
            case PrefabTypes.SolidRectangle:
                result = LevelObjectManipulator.SpawnSolidRectangle(pos, Color.White);
                break;
            case PrefabTypes.Player:
                result = PlayerManipulator.SpawnPlayer(pos, 0);
                break;
            case PrefabTypes.RegularSolidTile:
                if (!spawnInfo.VisualFromSetID.HasValue)
                {
                    goto default;
                }
                result = TileManipulator.SpawnRegularSolidTile(
                    pos, 
                    (TileID)spawnInfo.VisualFromSetID.Value
                );
                break;
            case PrefabTypes.VisualTile:
                if (!spawnInfo.VisualFromSetID.HasValue)
                {
                    goto default;
                }
                result = TileManipulator.SpawnVisualTile(
                    pos, 
                    (TileID)spawnInfo.VisualFromSetID.Value
                );
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

        if (maybeSpawnInfoOverrides.HasValue)
        {
            /*if (ExtraSpawnInfo.ContainsKey(FiledEntity.ExtraDataTypes.SpriteAnimOverride))
            {
                Set(result, (SpriteAnimation)ExtraSpawnInfo[FiledEntity.ExtraDataTypes.SpriteAnimOverride]);
            }*/
            // FIXME: Apply layer color?
            if (maybeSpawnInfoOverrides.Value.ColorBlend.HasValue)
            {
                Set(result, maybeSpawnInfoOverrides.Value.ColorBlend.Value);
            }
            if (maybeSpawnInfoOverrides.Value.Angle.HasValue)
            {
                Set(result, maybeSpawnInfoOverrides.Value.Angle.Value);
            }
        }

        if (rememberCreationForUndo)
        {
            UndoRedo.RememberEntityCreation(result, World);
        }

        return result;
    }

#if DEBUG

    public bool IsDefaultSprite(SpriteAnimation spriteToCheck, PrefabTypes prefabType)
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
    public bool IsDefaultColorBlend(Color colorBlend, PrefabTypes prefabType)
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

    private PrefabTypes PrefabToSpawn_ForPreview = PrefabTypes.None;

    private void SetUpSelectedPrefabPreviewVisuals(Entity debugEntity)
    {
        if (!ImGui.GetIO().WantCaptureMouse)
        {
            // Spawn a copy of the prefab, then extract its visual info.
            var dummyPrefab = TrySpawnPrefab(PrefabToSpawn_ForPreview, Input.WorldMousePosition, true).Value;

            Set(debugEntity, Get<Position2D>(dummyPrefab));
            if (Has<VisualScale>(dummyPrefab))
            {
                Set(debugEntity, Get<VisualScale>(dummyPrefab));
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
                Set(debugEntity, Angle.FromRadians(
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
            Remove<VisualScale>(debugEntity);
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
            foreach (PrefabTypes prefab in Enum.GetValues(typeof(PrefabTypes)))
            {
                if (prefab == PrefabTypes.None)
                {
                    continue;
                }
                if (prefab >= PrefabTypes.SPAWNED_NORMALLY_COUNT)
                {
                    continue;
                }

                bool isSelected = PrefabToSpawn_ForPreview == prefab;
                ImGui.PushStyleColor(ImGuiCol.Header, Color.Green.ToVector4());
                if (ImGui.Selectable(prefab.ToString(), isSelected))
                {
                    PrefabToSpawn_ForPreview = isSelected ? PrefabTypes.None : prefab;
                }
                ImGui.PopStyleColor();
            }

            // TODO: Once button to spawn a prefab entity is pressed, make it appear transparent below cursor.
            if (PrefabToSpawn_ForPreview != PrefabTypes.None)
            {
                if (!ImGui.GetIO().WantCaptureMouse
                    && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    TrySpawnPrefab(PrefabToSpawn_ForPreview, Input.WorldMousePosition);
                }
            }
        }
        ImGui.End();

        if (PrefabToSpawn_ForPreview != PrefabTypes.None)
        {
            SetUpSelectedPrefabPreviewVisuals(debugEntity);
            return true;
        }
        return false;
    }
#endif
}