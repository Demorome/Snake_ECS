using System;
using Hexa.NET.ImGui;
using MoonTools.ECS;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Systems;

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

    public void ShowPrefabSpawner()
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
    }
}