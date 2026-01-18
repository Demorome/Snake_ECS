using System.Text.Json;
using System.Text.Json.Serialization;
using MoonTools.ECS;
using MoonWorks;
using RollAndCash.Content;

#if DEBUG
using RollAndCash.Editor;
#endif

namespace RollAndCash.Data;

public static class LevelSerialization
{
    static JsonSerializerOptions LevelSerializerOptions 
        = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

    static FiledWorldContext JsonLevelContext = new(LevelSerializerOptions);

#if DEBUG
    // MARK: Save level
    public static void SaveToFile(
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
                    if (!world.Has<PrefabID>(liveEntity))
                    {
                        Logger.LogError($"{Systems.EditorSystem.EntityToString(world, liveEntity)} in layer {liveLayer.Name} has no PrefabID. We won't save it!");
                        continue;
                    }
                    var filedEntity = FiledEntity.Editor_FromLiveEntity(
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
                    _ = filedEntity.ToLiveEntity(
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
}