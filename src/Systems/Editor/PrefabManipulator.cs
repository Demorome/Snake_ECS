using System;
using System.Diagnostics;
using Hexa.NET.ImGui;
using MoonTools.ECS;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Systems;
using RollAndCash.Utility;

namespace RollAndCash.Editor;

public class PrefabManipulator : MoonTools.ECS.Manipulator
{
    MirrorManipulator MirrorManipulator;
    EnemySpawner EnemySpawner;

    public PrefabManipulator(World world) : base(world)
    {
        MirrorManipulator = new(world);
        EnemySpawner = new(world);
    }

    private enum Prefab
    {
        None = 0,
        StaticLevelMirror,
        FrogEnemy
    }
    private Prefab PrefabToSpawn = Prefab.None;

    private Entity SpawnPrefab(Position2D pos)
    {
        // FIXME: Add to change history!

        switch (PrefabToSpawn)
        {
            case Prefab.StaticLevelMirror:
                return MirrorManipulator.CreateStaticLevelMirror(pos);
            case Prefab.FrogEnemy:
                return EnemySpawner.SpawnFrog(pos);
        }
        throw new Exception("Failed to spawn prefab");
    }

    private void SetUpSelectedPrefabPreviewVisuals(Entity debugEntity)
    {
        if (!ImGui.GetIO().WantCaptureMouse)
        {
            // Spawn a copy of the prefab, then extract its visual info.
            var dummyPrefab = SpawnPrefab(Input.WorldMousePosition);

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
            foreach (Prefab prefab in Enum.GetValues(typeof(Prefab)))
            {
                if (prefab == Prefab.None)
                {
                    continue;
                }

                bool isSelected = PrefabToSpawn == prefab;
                ImGui.PushStyleColor(ImGuiCol.Header, Color.Green.ToVector4());
                if (ImGui.Selectable(prefab.ToString(), isSelected))
                {
                    PrefabToSpawn = isSelected ? Prefab.None : prefab;
                }
                ImGui.PopStyleColor();
            }

            // TODO: Once button to spawn a prefab entity is pressed, make it appear transparent below cursor.
            if (PrefabToSpawn != Prefab.None)
            {
                if (!ImGui.GetIO().WantCaptureMouse
                    && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    SpawnPrefab(Input.WorldMousePosition);
                }
            }
        }
        ImGui.End();

        if (PrefabToSpawn != Prefab.None)
        {
            SetUpSelectedPrefabPreviewVisuals(debugEntity);
            return true;
        }
        return false;
    }
}