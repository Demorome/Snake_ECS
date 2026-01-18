using System.Text.Json;
using System.Text.Json.Serialization;
using MoonTools.ECS;
using MoonWorks;
using RollAndCash.Components;
using RollAndCash.Content;

#if DEBUG
using RollAndCash.Editor;
#endif

namespace RollAndCash.Data;

// This is to optimize JSON serializing w/ source generation: 
// https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation
[JsonSerializable(typeof(FiledGameInfo))]
[JsonSerializable(typeof(FiledLevel))]
internal partial class FiledWorldContext : JsonSerializerContext
{
}

public static class LevelSerialization
{
    static JsonSerializerOptions LevelSerializerOptions 
        = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

    static FiledWorldContext JsonLevelContext 
        = new(LevelSerializerOptions);

#if DEBUG
    // MARK: Save level
    public static void Editor_SaveToFile(
        LiveLevel liveLevel,
        string levelContentPath, 
        World world, 
        PrefabManipulator prefabManipulator
        )
    {
        var filedLevel = new FiledLevel();
        filedLevel.SerializedVersion = 1;
        filedLevel.Name = liveLevel.Name;

        List<FiledLevel.Room> filedRooms = new(liveLevel.Rooms.Count);
        foreach (var liveRoom in liveLevel.Rooms)
        {
            var filedRoom = new FiledLevel.Room();

            List<FiledLevel.Layer> filedLayers = new(liveRoom.LayersByName.Count);
            foreach (var (_, liveLayer) in liveRoom.LayersByName)
            {
                var filedLayer = new FiledLevel.Layer(liveLayer);

                var filedEntities = new List<FiledEntity>(
                    liveLayer.CachedEntities.Count
                );
                foreach (var liveEntity in liveLayer.CachedEntities)
                {
                    if (!world.Has<Editor_PrefabID>(liveEntity))
                    {
                        Logger.LogError($"{Systems.EditorSystem.EntityToString(world, liveEntity)} in layer {liveLayer.Name} has no PrefabID. We won't save it!");
                        continue;
                    }
                    var filedEntity = Editor_SaveEntity(
                        liveEntity,
                        world,
                        liveRoom,
                        liveLayer,
                        prefabManipulator
                    );

                    filedEntities.Add(filedEntity);
                }
                filedLayer.Entities = filedEntities.ToArray();
                filedLayers.Add(filedLayer);
            }
            filedRoom.Layers = filedLayers.ToArray();
            filedRooms.Add(filedRoom);
        }

        filedLevel.Rooms = filedRooms.ToArray();

        var json = JsonSerializer.Serialize(
            filedLevel, 
            typeof(FiledLevel), 
            JsonLevelContext
        );
        Directory.CreateDirectory(levelContentPath);
        var jsonOutputPath = Path.Combine(
            levelContentPath, 
            liveLevel.Name + ".json"
        );
        File.WriteAllText(jsonOutputPath, json);
    }
#endif
    
    // MARK: Load level
    public static LiveLevel LoadFromFile(
        string jsonPath, 
        World world, 
        PrefabManipulator prefabManipulator)
    {
#if DEBUG
        UndoRedo.ClearChangeHistoryList();
        UndoRedo.ClearRedoList();
#endif

        var filedLevel = (FiledLevel)JsonSerializer.Deserialize(
            File.ReadAllText(jsonPath),
            typeof(FiledLevel),
            JsonLevelContext
        )!;

        // 'SerializedVersion' can be used here, if needed

        var liveLevelResult = new LiveLevel();
        liveLevelResult.Name = filedLevel.Name;

        foreach (var filedRoom in filedLevel.Rooms)
        {
            // Adds itself to list in ctor
            var liveRoom = new LiveLevel.Room(filedRoom, liveLevelResult);

            foreach (var filedLayer in filedRoom.Layers)
            {
#if DEBUG
                if (liveRoom.LayersByName.ContainsKey(filedLayer.EditorName!))
                {
                    Logger.LogError($"Unable to load layer {filedLayer.EditorName}: Name is no longer unique w/ other layers in this room!");
                    continue;
                }
                var liveEditorLayer = new LiveLevel.EditorLayer(
                    filedLayer, 
                    liveRoom
                ); // adds itself to lists in ctor
#endif
                VisualSetID? maybeTileSetID = null;

                var logSetNotFoundError = (string setName) => 
                    Logger.LogError($"Unable to load layer {filedLayer.EditorName}: Couldn't find visual set {setName}");

                var logVariantNotFoundError = (byte variantID) => 
                    Logger.LogError($"Unable to load layer {filedLayer.EditorName}: Couldn't find visual set variant with ID {variantID}");

                if (filedLayer.TypeID == LevelLayerTypes.TileSet)
                {
                    if (!TileSets.NameToTileSet.ContainsKey(
                        filedLayer.MaybeVisualSet!.Value.NameID))
                    {
                        logSetNotFoundError(filedLayer.MaybeVisualSet.Value.NameID);
                        continue;
                    }
                    var tileSet = TileSets.NameToTileSet[
                        filedLayer.MaybeVisualSet.Value.NameID
                    ];

                    // Reminder that a variantID of 0 is valid; 
                    // it means the default tileset.
                    if (tileSet.VariantSets.Count 
                        > filedLayer.MaybeVisualSet.Value.VariantID)
                    {
                        logVariantNotFoundError(filedLayer.MaybeVisualSet.Value.VariantID);
                    }
                    maybeTileSetID = tileSet.ID;
                }
                else if (filedLayer.TypeID == LevelLayerTypes.ImageSet)
                {
                    // FIXME: Implement!
                }

                for (int nthEntity = 0; 
                    nthEntity < filedLayer.Entities.Length; 
                    ++nthEntity
                )
                {
                    var filedEntity = filedLayer.Entities[nthEntity];
                    _ = LoadEntity(
                        filedEntity,
                        filedRoom,
                        filedLayer,
                        maybeTileSetID,
                        world, 
                        prefabManipulator,
                        liveRoom
#if DEBUG
                        , liveEditorLayer
#endif
                    );
                }
            }
        }
        
        return liveLevelResult;
    }

    //MARK: Load Entity
    public static Entity? LoadEntity(
        FiledEntity toLoad,
        FiledLevel.Room filedRoom,
        FiledLevel.Layer filedLayer,
        VisualSetID? maybeTileSetID,
        World world,
        PrefabManipulator prefabManipulator,
        LiveLevel.Room liveRoom
#if DEBUG
        , LiveLevel.EditorLayer liveEditorLayer
#endif
    )
    {
        PrefabType prefabType;
        PrefabSpawnInfo_Processed spawnInfo = new();

        if (LevelLayerTypesFuncs.IsVisualSet(filedLayer.TypeID))
        {
            var visualFromSetID = new VisualFromSetID_ForSpawning(
                toLoad.MaybeSpawnInfo!.Value.PosInVisualSet!.Value, 
                maybeTileSetID!.Value, 
                new VisualSetVariantID(
                    filedLayer.MaybeVisualSet!.Value.VariantID
                )
            );

            (prefabType, 
                var maybeSpawnFlags, 
                var maybeExtraSpawnInfo_FromVisualSet
                ) = VisualSet.GetMetadata(visualFromSetID);

            spawnInfo = new PrefabSpawnInfo_Processed(visualFromSetID);
            if (!toLoad.MaybeSpawnFlags.HasValue)
            {
                toLoad.MaybeSpawnFlags = maybeSpawnFlags;
            }

            if (toLoad.MaybeSpawnInfoOverrides == null)
            {
                toLoad.MaybeSpawnInfoOverrides 
                    = maybeExtraSpawnInfo_FromVisualSet;
            }
        }
        else if (filedLayer.TypeID == LevelLayerTypes.Prefabs)
        {
            prefabType = filedLayer.MaybePrefabTypeForEntities!.Value;
        }
        else
        {
            Logger.LogError($"Bad layer type: {filedLayer.TypeID}");
            return null;
        }

        var spawnPosition 
            = toLoad.PositionRelativeToRoom + filedRoom.Position;

        // Spawn the entities
        var maybeLiveEntity = prefabManipulator.TrySpawnPrefab(
            prefabType,
            spawnPosition,
            false,
            spawnInfo,
            toLoad.MaybeSpawnFlags.HasValue 
                ? toLoad.MaybeSpawnFlags.Value 
                : FiledEntity.Flags.None,
            toLoad.MaybeSpawnInfoOverrides
        );

        if (maybeLiveEntity.HasValue)
        {
            var liveEntity = maybeLiveEntity.Value;
            world.Set(liveEntity, liveRoom.ID);
            world.Set(liveEntity, new Depth(filedLayer.Depth));

            if (toLoad.UniqueTag != null 
                && toLoad.UniqueTag.Length != 0)
            {
                world.Tag(liveEntity, toLoad.UniqueTag);
            }

#if DEBUG                      
            world.Set(liveEntity, liveEditorLayer.LayerID);
            liveEditorLayer.CachedEntities.Add(liveEntity);
#endif
        }
        return maybeLiveEntity;
    }


#if DEBUG
    //MARK: Save Entity
    public static FiledEntity Editor_SaveEntity(
        Entity liveEntity,
        World world,
        LiveLevel.Room liveRoom,
        LiveLevel.EditorLayer liveLayer,
        PrefabManipulator prefabManipulator
    )
    {
        var filedEntity = new FiledEntity();
        filedEntity.PositionRelativeToRoom = new Position2D(
            world.Get<Position2D>(liveEntity) - liveRoom.Position
        );

        // Set spawn info
        var spawnInfoForDummy = new PrefabSpawnInfo_Processed();
        {
            var filedSpawnInfo = new PrefabSpawnInfo_Filed();

            bool empty = true;
            if (liveLayer.LayerType == LevelLayerTypes.TileSet)
            {
                var tileID = world.Get<TileID>(liveEntity);
                spawnInfoForDummy.VisualFromSetID = (VisualFromSetID_ForSpawning)tileID;
                filedSpawnInfo.PosInVisualSet = tileID.PosInSet;
                empty = false;
            }
            else if (liveLayer.LayerType == LevelLayerTypes.ImageSet)
            {
                // TODO!
                empty = false;
            }
            else if (liveLayer.LayerType == LevelLayerTypes.Prefabs)
            {
                // TODO! If we have a prefab that needs an arg to be spawned.
                empty = false;
            }

            if (!empty)
            {
                filedEntity.MaybeSpawnInfo = filedSpawnInfo;
            }
        }

        // Compare our liveEntity with this basic version, 
        // to see what was changed over the default creation code.
        // Could hardcode it, which would be more efficient speed-wise,
        // but less efficient time-wise. Plus, this is editor code.
        var dummyEntity = prefabManipulator.TrySpawnPrefab(
            world.Get<Editor_PrefabID>(liveEntity).ID,
            world.Get<Position2D>(liveEntity),
            false,
            spawnInfoForDummy
        )!.Value;
        
        // Set spawn flags
        {
            bool different = false;
            var spawnFlags = FiledEntity.Flags.None;

            if (CompareEntityFlagComponent<HorizontalFlip>(liveEntity, dummyEntity, world, ref different))
            {
                spawnFlags |= FiledEntity.Flags.FlipX;
            }
            if (CompareEntityFlagComponent<VerticalFlip>(liveEntity, dummyEntity, world, ref different))
            {
                spawnFlags |= FiledEntity.Flags.FlipY;
            }

            if (different)
            {
                filedEntity.MaybeSpawnFlags = spawnFlags;
            }
        }

        // Set spawn info overrides
        {
            bool different = false;
            var spawnInfoOverrides = new PrefabSpawnInfoOverride();

            // FIXME!!
            if (world.Has<ColorBlend>(liveEntity))
            {
                spawnInfoOverrides.ColorBlend = world.Get<ColorBlend>(liveEntity);
                different = true;
            }

            /* FIXME: Save direction
            if (world.Has<RotatesWithDirection>(liveEntity))
            {
                angle = MathUtilities.AngleFromUnitVector(world.Get<Direction2D>(liveEntity).Value);
            }*/

            if (world.Has<Angle>(liveEntity))
            {
                var angle = world.Get<Angle>(liveEntity);
                if (angle.ValueInRadians != 0.0f)
                {
                    spawnInfoOverrides.Angle = angle;
                    different = true;
                }
            }

            if (different)
            {
                filedEntity.MaybeSpawnInfoOverrides = spawnInfoOverrides;
            }
        }

        world.Destroy(dummyEntity);
        
        return filedEntity;
    }

    /// <summary>
    /// Return true if entity 'a' has the component and 'b' doesn't.
    /// Thus, we can add the flag to 'a' to override the 'b' parent.
    /// </summary>
    private static bool CompareEntityFlagComponent<T>(
        in Entity child, 
        in Entity parent, 
        World w, 
        ref bool different
    ) where T : unmanaged
    {
        if (w.Has<T>(child) && !w.Has<T>(parent))
        {
            different = true;
            return true;
        }
        if (!w.Has<T>(child) && w.Has<T>(parent))
        {
            different = true;
        }
        return false;
    }
#endif
}