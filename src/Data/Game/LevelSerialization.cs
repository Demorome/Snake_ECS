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
    public static void Editor_SaveLevelToFile(
        LoadedLevel liveLevel,
        string levelContentPath, 
        World world, 
        PrefabManipulator prefabManipulator
        )
    {
        var filedLevel = new FiledLevel();
        filedLevel.SerializedVersion = 1;
        filedLevel.PlayerFacingName = liveLevel.PlayerFacingName;
        filedLevel.ID = liveLevel.ID;

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
                        Logger.LogError($"{EntityExt.EntityToString(world, liveEntity)} in layer {liveLayer.Name} has no PrefabID. We won't save it!");
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

        if (world.Some<LevelStart>())
        {
            var levelStart = world.GetSingleton<LevelStart>();

            filedLevel.StartingRoomID = levelStart.StartRoomID;

            // FIXME: Also store the position in the FiledLevel directly, 
            // instead of saving it as an entity?

            // FIXME: What about saving + loading room portal links??
        }
        else
        {
            filedLevel.StartingRoomID = default;
        }

        var json = JsonSerializer.Serialize(
            filedLevel, 
            typeof(FiledLevel), 
            JsonLevelContext
        );
        Directory.CreateDirectory(levelContentPath);
        var jsonOutputPath = Path.Combine(
            levelContentPath, 
            liveLevel.PlayerFacingName + ".json"
        );
        File.WriteAllText(jsonOutputPath, json);
    }
#endif
    
    // MARK: Load level
    /// <summary>
    /// Fully loads the level, including all its rooms. <br/>
    /// All loaded entities that aren't in the starting room
    /// are Disabled by default.
    /// </summary>
    public static LoadedLevel LoadLevelFromFile(
        string jsonPath, 
        World world, 
        PrefabManipulator prefabManipulator)
    {
#if DEBUG
        UndoRedo.ClearChangeHistoryList();
        UndoRedo.ClearRedoList();
#endif

        var levelToLoad = (FiledLevel)JsonSerializer.Deserialize(
            File.ReadAllText(jsonPath),
            typeof(FiledLevel),
            JsonLevelContext
        )!;

        // 'SerializedVersion' can be used here, if needed

        var liveLevelResult = new LoadedLevel()
        {
            PlayerFacingName = levelToLoad.PlayerFacingName,
            StartingRoomID = levelToLoad.StartingRoomID,
            ID = levelToLoad.ID
        };

        foreach (var roomToLoad in levelToLoad.Rooms)
        {
            // Adds itself to list in ctor
            var liveRoom = new LoadedLevel.Room(roomToLoad, liveLevelResult);

            foreach (var layerToLoad in roomToLoad.Layers)
            {
#if DEBUG
                if (liveRoom.LayersByName.ContainsKey(layerToLoad.EditorName!))
                {
                    Logger.LogError($"Unable to load layer {layerToLoad.EditorName}: Name is no longer unique w/ other layers in this room!");
                    continue;
                }
                var liveEditorLayer = new LoadedLevel.EditorLayer(
                    layerToLoad, 
                    liveRoom
                ); // adds itself to lists in ctor
#endif
                VisualSetID? maybeTileSetID = null;

                var logSetNotFoundError = (string setName) => 
                    Logger.LogError($"Unable to load layer {layerToLoad.EditorName}: Couldn't find visual set {setName}");

                var logVariantNotFoundError = (byte variantID) => 
                    Logger.LogError($"Unable to load layer {layerToLoad.EditorName}: Couldn't find visual set variant with ID {variantID}");

                if (layerToLoad.TypeID == LevelLayerTypes.TileSet)
                {
                    if (!TileSets.NameToTileSet.ContainsKey(
                        layerToLoad.MaybeVisualSet!.Value.NameID))
                    {
                        logSetNotFoundError(layerToLoad.MaybeVisualSet.Value.NameID);
                        continue;
                    }
                    var tileSet = TileSets.NameToTileSet[
                        layerToLoad.MaybeVisualSet.Value.NameID
                    ];

                    // Reminder that a variantID of 0 is valid; 
                    // it means the default tileset.
                    if (tileSet.VariantSets.Count 
                        > layerToLoad.MaybeVisualSet.Value.VariantID)
                    {
                        logVariantNotFoundError(layerToLoad.MaybeVisualSet.Value.VariantID);
                    }
                    maybeTileSetID = tileSet.ID;
                }
                else if (layerToLoad.TypeID == LevelLayerTypes.ImageSet)
                {
                    // FIXME: Implement!
                }

                for (int nthEntity = 0; 
                    nthEntity < layerToLoad.Entities.Length; 
                    ++nthEntity
                )
                {
                    var entityToLoad = layerToLoad.Entities[nthEntity];

                    var maybeLiveEntity = LoadEntity(
                        entityToLoad,
                        roomToLoad,
                        layerToLoad,
                        maybeTileSetID,
                        world, 
                        prefabManipulator,
                        liveRoom
#if DEBUG
                        , liveEditorLayer
#endif
                    );

                    if (maybeLiveEntity != null)
                    {
                        var liveEntity = maybeLiveEntity.Value;
                        
                        if (liveLevelResult.StartingRoomID != liveRoom.ID)
                        {
                            world.Set(liveEntity, new Disabled());
                        }
                    }
                }
            }
        }
        
        return liveLevelResult;
    }

    //MARK: Load Entity
    private static Entity? LoadEntity(
        FiledEntity toLoad,
        FiledLevel.Room filedRoom,
        FiledLevel.Layer filedLayer,
        VisualSetID? maybeTileSetID,
        World world,
        PrefabManipulator prefabManipulator,
        LoadedLevel.Room liveRoom
#if DEBUG
        , LoadedLevel.EditorLayer liveEditorLayer
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

            (   prefabType, 
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

        // Spawn the entity
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
    private static FiledEntity Editor_SaveEntity(
        Entity liveEntity,
        World world,
        LoadedLevel.Room liveRoom,
        LoadedLevel.EditorLayer liveLayer,
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
                spawnInfoForDummy.VisualFromSetID 
                    = (VisualFromSetID_ForSpawning)tileID;
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

            if (CompareEntityFlagComponent<HorizontalFlip>(
                liveEntity, 
                dummyEntity, 
                world, 
                ref different))
            {
                spawnFlags |= FiledEntity.Flags.FlipX;
            }
            if (CompareEntityFlagComponent<VerticalFlip>(
                liveEntity, 
                dummyEntity, 
                world, 
                ref different))
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
                spawnInfoOverrides.ColorBlend 
                    = world.Get<ColorBlend>(liveEntity);
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