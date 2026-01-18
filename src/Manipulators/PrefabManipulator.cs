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
        PrefabType prefabType,
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
            case PrefabType.StaticLevelMirror:
                result = MirrorManipulator.CreateStaticLevelMirror(pos);
                break;
            case PrefabType.FrogEnemy:
                result = PlayerManipulator.SpawnFrog(pos);
                break;
            case PrefabType.InvisibleSolidRectangle:
                result = LevelObjectManipulator.SpawnInvisibleSolidRectangle(pos);
                break;
            case PrefabType.SolidRectangle:
                result = LevelObjectManipulator.SpawnSolidRectangle(pos, Color.White);
                break;
            case PrefabType.Player:
                result = PlayerManipulator.SpawnPlayer(pos, 0);
                break;
            case PrefabType.RegularSolidTile:
                if (!spawnInfo.VisualFromSetID.HasValue)
                {
                    goto default;
                }
                result = TileManipulator.SpawnRegularSolidTile(
                    pos, 
                    (TileID)spawnInfo.VisualFromSetID.Value
                );
                break;
            case PrefabType.VisualTile:
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
        
        if (!Has<Editor_PrefabID>(result))
        {
            //Logger.LogError($"Spawned prefab {prefabType} should have received a prefabID!");
            Set(result, new Editor_PrefabID(prefabType));
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

    public bool IsDefaultSprite(SpriteAnimation spriteToCheck, PrefabType prefabType)
    {
        bool result;
        var dummyPrefab = TrySpawnPrefab(prefabType, Input.WorldMousePosition)!.Value;
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
    public bool IsDefaultColorBlend(Color colorBlend, PrefabType prefabType)
    {
        bool result;
        var dummyPrefab = TrySpawnPrefab(prefabType, Input.WorldMousePosition)!.Value;
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

    private void SetUpSelectedPrefabPreviewVisuals(
        PrefabType prefabToSpawn,
        Entity debugEntity
        )
    {
        if (!ImGui.GetIO().WantCaptureMouse)
        {
            // Spawn a copy of the prefab, then extract its visual info.
            var dummyPrefab = TrySpawnPrefab(
                prefabToSpawn, Input.WorldMousePosition, true
            )!.Value;

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
            // FIXME: Just use a different debug entity here and destroy it instead!
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
    public void ShowPrefabSpawnerAndMaybeSpawn(
        ref PrefabType prefabToSpawn,
        Entity debugEntity
    )
    {
        if (ImGui.Begin("Prefab Objects"u8))
        {
            foreach (PrefabType prefab in Enum.GetValues(typeof(PrefabType)))
            {
                if (prefab == PrefabType.None)
                {
                    continue;
                }
                if (prefab >= PrefabType.SPAWNED_NORMALLY_COUNT)
                {
                    continue;
                }

                bool isSelected = prefabToSpawn == prefab;
                ImGui.PushStyleColor(ImGuiCol.Header, Color.Green.ToVector4());
                if (ImGui.Selectable(prefab.ToString(), isSelected))
                {
                    prefabToSpawn = isSelected ? PrefabType.None : prefab;
                }
                ImGui.PopStyleColor();
            }

            // TODO: Once button to spawn a prefab entity is pressed, make it appear transparent below cursor.
            if (prefabToSpawn != PrefabType.None)
            {
                if (!ImGui.GetIO().WantCaptureMouse
                    && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    TrySpawnPrefab(prefabToSpawn, Input.WorldMousePosition);
                }
            }
        }
        ImGui.End();

        if (prefabToSpawn != PrefabType.None)
        {
            SetUpSelectedPrefabPreviewVisuals(prefabToSpawn, debugEntity);
        }
    }
#endif
}