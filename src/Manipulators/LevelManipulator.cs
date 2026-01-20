using System;
using MoonTools.ECS;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Data;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class LevelManipulator : MoonTools.ECS.Manipulator
{
    public static LoadedLevel? ActiveLevel { get; private set; }
    public static LoadedLevel.Room? ActiveRoom { get; private set; }

    public bool TransitioningBetweenRooms { get; private set; }
    public bool TransitioningBetweenLevels { get; private set; }

    static readonly string LevelContentPath =
       Path.Combine("Content", "Levels");

    PrefabManipulator PrefabManipulator;

    public LevelManipulator(World world) : base(world)
    {
        PrefabManipulator = new(World);
    }

    public void LoadLevel(LevelType levelType)
    {
        MaybeUnloadActiveLevel();

        // FIXME: Access some LevelList to get string instead!
        var levelPathStr = Path.Combine(
            LevelContentPath,
            levelType.ToString()
        );

        LevelSerialization.LoadLevelFromFile(
            levelPathStr, 
            World, 
            PrefabManipulator
        );
    }

    public void MaybeUnloadActiveLevel()
    {
        if (ActiveLevel == null)
        {
            return;
        }

        ActiveLevel = null;
        ActiveRoom = null;
    }

#if DEBUG
    /// <summary>
    /// ActiveLevel must NOT be null!
    /// </summary>
    public void Editor_SaveActiveLevel()
    {
        // TODO: Create backups of previous level file if possible!
        LevelSerialization.Editor_SaveLevelToFile(
            ActiveLevel!, 
            LevelContentPath, 
            World, 
            PrefabManipulator
        );
    }
#endif
}