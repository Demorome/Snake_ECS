using System;
using System.Diagnostics;
using Hexa.NET.ImGui;
using MoonTools.ECS;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Systems;
using RollAndCash.Utility;
using RollAndCash;


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
    SPAWNED_NORMALLY_COUNT,

    //== These shouldn't be spawned manually, since they rely on vital info from another source anyways.
    // They are here solely to tag special entities for save/loading.
    SolidTile,
    VisualTile,
    Image
}
public readonly record struct PrefabID(Prefabs ID);

public class PrefabManipulator : MoonTools.ECS.Manipulator
{
    MirrorManipulator MirrorManipulator;
    EnemySpawner EnemySpawner;

    public PrefabManipulator(World world) : base(world)
    {
        MirrorManipulator = new(world);
        EnemySpawner = new(world);
    }

    public Entity SpawnPrefab(Prefabs prefabType, Position2D pos,
        bool isDummy = false, bool persistent = false)
    {
        Entity result;
        switch (prefabType)
        {
            case Prefabs.StaticLevelMirror:
                result = MirrorManipulator.CreateStaticLevelMirror(pos);
                break;
            case Prefabs.FrogEnemy:
                result = EnemySpawner.SpawnFrog(pos);
                break;
            case Prefabs.SolidRectangle:
                result = CreateEntity();
                Set(result, new Rectangle(0, 0, Dimensions.TILE_SIZE, Dimensions.TILE_SIZE));
                Set(result, new DrawAsRectangle());
                Set(result, new ColorBlend(Color.White));
                break;
            case Prefabs.InvisibleSolidRectangle:
                result = CreateEntity();
                Set(result, new Rectangle(0, 0, Dimensions.TILE_SIZE, Dimensions.TILE_SIZE));
                break;
                
            //=== These rely upon something else setting up their appearance.
            case Prefabs.SolidTile:
                result = CreateEntity();
                Set(result, new Rectangle(0, 0, Dimensions.TILE_SIZE, Dimensions.TILE_SIZE));
                break;
            case Prefabs.VisualTile:
                result = CreateEntity();
                break;
            case Prefabs.Image:
                result = CreateEntity();
                break;

            default:
                throw new Exception("Failed to spawn prefab");
        }

        if (GetTag(result).Length == 0)
        {
            Tag(result, prefabType.ToString());
        }
        Set(result, new PrefabID(prefabType));
        
        if (!persistent)
        {
            Set(result, new DestroyOnLevelReset());
        }

        if (!isDummy)
        {
            UndoRedo.RememberEntityCreation(result, World);
        }

        return result;
    }

#if DEBUG

    public bool IsDefaultSprite(SpriteAnimation spriteToCheck, Prefabs prefabType)
    {
        bool result;
        var dummyPrefab = SpawnPrefab(prefabType, Input.WorldMousePosition);
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

    private Prefabs PrefabToSpawn_ForPreview = Prefabs.None;

    private void SetUpSelectedPrefabPreviewVisuals(Entity debugEntity)
    {
        if (!ImGui.GetIO().WantCaptureMouse)
        {
            // Spawn a copy of the prefab, then extract its visual info.
            var dummyPrefab = SpawnPrefab(PrefabToSpawn_ForPreview, Input.WorldMousePosition, true);

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
                    SpawnPrefab(PrefabToSpawn_ForPreview, Input.WorldMousePosition);
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