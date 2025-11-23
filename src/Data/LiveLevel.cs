using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Relations;
using RollAndCash.Systems;
using RollAndCash.Utility;

#if DEBUG
using RollAndCash.Editor;
#endif

namespace RollAndCash.Data;

// Each level has multiple rooms.
// These rooms are self-contained gameplay areas with no spatial reference to other rooms.
// Only a single room will be loaded/active at any given time.
public readonly record struct LevelRoomID(int ID);

#if DEBUG
// Each room has multiple layers, which are a group of entities and tiles for the editor. 
public readonly record struct Editor_LevelLayerID(int ID);
#endif

// Represents a level that's fully loaded in-game.
public class LiveLevel
{
    public string Name = "";
    public readonly List<Room> Rooms = new();

    public class Room
    {
        public LevelRoomID ID;
        public readonly LiveLevel Level;
        public string Name;
        public Position2D Position; // top-left corner
        public int Width = Dimensions.GAME_W;
        public int Height = Dimensions.GAME_H;

#if DEBUG
        public readonly Dictionary<string, EditorLayer> Layers;
        public readonly List<EditorLayer> LayerIDs;
#endif

        public Room(LiveLevel levelParent)
        {
            Level = levelParent;
            lock (levelParent.Rooms)
            {
                ID = new LevelRoomID(levelParent.Rooms.Count);
                levelParent.Rooms.Add(this);
            }
#if DEBUG
            Layers = new();
            LayerIDs = new();
#endif
        }

        public Room(FiledLevel.Room filedRoom, LiveLevel levelParent)
        {
            Level = levelParent;
            lock (levelParent.Rooms)
            {
                ID = new LevelRoomID(levelParent.Rooms.Count);
                levelParent.Rooms.Add(this);
            }

            Name = filedRoom.Name;
            Position = filedRoom.Position;
            Width = filedRoom.W;
            Height = filedRoom.H;

#if DEBUG
            Layers = new(filedRoom.Layers.Length);
            LayerIDs = new(filedRoom.Layers.Length);
#endif
        }

#if DEBUG
        public EditorLayer GetLayerFromID(Editor_LevelLayerID layerID)
        {
            return LayerIDs[layerID.ID];
        }

        public void ValidateLayerName(ref string name)
        {
            if (!Layers.ContainsKey(name))
            {
                return;
            }

            name += " ";
            int i = 1;
            var testName = name + i.ToString();

            lock (Layers)
            {
                while (Layers.ContainsKey(testName))
                {
                    ++i;
                    testName = name + i.ToString();
                }
            }
            name = testName;
        }

        public void DeleteLayerCleanup(string layerToRemove, World world)
        {
            // FIXME: Undo support!
            // FIXME: If undone, need to re-apply relationship data too.
            // Ex: DebugEntiy DontDraw relation, if the layer was made invisible.

            // Deleting a layer deletes all entities in it.
            foreach (var entity in Layers[layerToRemove].CachedEntities)
            {
                world.Destroy(entity);
            }

            // Preserve the ID in the lookup, in case we want to undo this change.
            // May as well preserve the LevelLayer here too...?
            //IDLookup[LevelLayers[layerToRemove].LayerID.ID] = null;

            Layers.Remove(layerToRemove);
        }
#endif
    }

    public static Color MixLayerColorWithEntityColor(Color layerColor, Color entityColorBlend)
    {
        return Color.Lerp(layerColor, entityColorBlend, 0.5f);
    }

#if DEBUG
    public class EditorLayer
    {
        public string Name { get; private set; }
        public readonly Editor_LevelLayerID LayerID;
        public readonly Room Room;
        public LiveLevel Level => Room.Level;
        public bool IsNameLocked => LayerType == LevelLayerTypes.Prefabs;
        public LevelLayerTypes LayerType { get; private set; }
        public bool IsTiled => LayerType == LevelLayerTypes.TileSet;

        // A layer either uses a visual set or a prefab type to spawn stuff.
        public TileSet MaybeTileSet;
        public byte? MaybeVisualSetVariantID;
        //public ImageSet MaybeImageSet;
        public Prefabs? MaybePrefabType;

        // Applies to all images/tiles.
        public Color Color { get; private set; } = Color.White;

        public float Depth { get; private set; } = (float)DepthLayer.DefaultDepth;
        public bool IsDepthLocked => LayerType == LevelLayerTypes.Prefabs;
        public bool IsVisible { get; private set; } = true;
        public List<Entity> CachedEntities = new();

        public EditorLayer(
            LevelLayerTypes layerType,
            LiveLevel.Room room,
            string name = null,
            float depth = (float)DepthLayer.DefaultDepth
            )
        {
            LayerType = layerType;
            Room = room;
            Depth = depth;

            if (name == null)
            {
                name = LayerTypeToString(LayerType);
            }
            Room.ValidateLayerName(ref name);
            Name = name;

            lock (room.Layers)
            {
                room.Layers.Add(Name, this);
            }
            lock (room.LayerIDs)
            {
                LayerID = new Editor_LevelLayerID(room.LayerIDs.Count);
                room.LayerIDs.Add(this);
            }
        }

        public EditorLayer(FiledLevel.Layer filedLayer, Room room)
        {
            LayerType = filedLayer.TypeID;
            Room = room;
            Depth = filedLayer.Depth;
            // NOTE: Assumes the layer's name uniqueness' was preserved...
            Name = filedLayer.EditorName;
            Color = filedLayer.Color;

            lock (room.Layers)
            {
                room.Layers.Add(Name, this);
            }
            lock (room.LayerIDs)
            {
                LayerID = new Editor_LevelLayerID(room.LayerIDs.Count);
                room.LayerIDs.Add(this);
            }
        }

        // Should never be called if this isn't a TileSet-type layer.
        public void ReplaceTileSet(TileSet newTileSet, World world)
        {
            MaybeTileSet = newTileSet;

            // Update visuals for every entity in this layer that was using the old one.
            foreach (var entity in CachedEntities)
            {
                if (!world.Has<TileID>(entity))
                {
                    Logger.LogError("Entity should have a TileID component here!");
                    continue;
                }
                var tileID = world.Get<TileID>(entity);

                world.Set(entity, tileID with {TileSetID = newTileSet.ID});
            }
        }

        public void ToggleVisibility(World world, Entity debugEntity)
        {
            IsVisible = !IsVisible;
            if (!IsVisible)
            {
                // Hide every entity in this layer
                foreach (var entity in CachedEntities)
                {
                    world.Relate(entity, debugEntity, new DontDraw());
                }
            }
            else
            {
                // Un-hide every entity in this layer
                foreach (var entity in CachedEntities)
                {
                    world.Unrelate<DontDraw>(entity, debugEntity);
                }
            }
        }

        public void ChangeLayerColorBlend(Color newColor, World world)
        {
            Color = newColor;

            // Recalculate the color blend for each entity in this layer.
            foreach (var entity in CachedEntities)
            {
                var entityColor = Color.White;
                if (world.Has<Editor_EntityBaseColorBlend>(entity))
                {
                    entityColor = world.Get<Editor_EntityBaseColorBlend>(entity).Color;
                }
                if (Color == Color.White && entityColor == Color.White)
                {
                    world.Remove<ColorBlend>(entity);
                    world.Remove<Editor_EntityBaseColorBlend>(entity);
                }
                else
                {
                    world.Set(entity, new ColorBlend(MixLayerColorWithEntityColor(Color, entityColor)));
                }
            }
        }

        public void ChangeLayerDepth(float newDepth, World world)
        {
            if (IsDepthLocked)
            {
                Logger.LogError("Shouldn't be able to change layer depth!");
                return;
            }

            foreach (var entity in CachedEntities)
            {
                world.Set(entity, new Depth(newDepth));
            }
            Depth = newDepth;
        }

        public static string LayerTypeToString(LevelLayerTypes layerType)
        {
            return layerType switch
            {
                LevelLayerTypes.Prefabs => "Prefab",
                LevelLayerTypes.ImageSet => "ImageSet",
                LevelLayerTypes.TileSet =>  "TileSet",
                LevelLayerTypes.Unknown => "Unknown/Dynamic",
                _ => "Invalid level layer type!"
            };
        }
    }
#endif

    static JsonSerializerOptions LevelSerializerOptions = new JsonSerializerOptions
    {
        IncludeFields = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static FiledWorldContext JsonLevelContext = new(LevelSerializerOptions);

#if DEBUG
    public void SaveToFile(string levelContentPath, World world, PrefabManipulator prefabManipulator)
    {
        var filedLevel = new FiledLevel();
        filedLevel.SerializedVersion = 1;
        filedLevel.Name = Name;

        List<FiledLevel.Room> filedRooms = new(Rooms.Count);
        foreach (var liveRoom in Rooms)
        {
            var filedRoom = new FiledLevel.Room();

            List<FiledLevel.Layer> filedLayers = new(liveRoom.Layers.Count);
            foreach (var (_, liveLayer) in liveRoom.Layers)
            {
                var filedLayer = new FiledLevel.Layer(liveLayer);

                var filedEntities = new List<FiledEntity>(liveLayer.CachedEntities.Count);
                foreach (var liveEntity in liveLayer.CachedEntities)
                {
                    if (!world.Has<PrefabID>(liveEntity))
                    {
                        Logger.LogWarn($"{EditorSystem.EntityToString(world, liveEntity)} in layer {liveLayer.Name} has no PrefabID. That's pretty weird!");
                    }

                    var filedEntity = new FiledEntity();
                    filedEntity.PositionRelativeToRoom = new Position2D(world.Get<Position2D>(liveEntity) - liveRoom.Position);
                    
                    // Set spawn flags
                    filedEntity.MaybeSpawnFlags = 
                        world.Has<Editor_EntityOverrideSpawnFlags>(liveEntity) ?
                        FiledEntity.Flags.None : null
                    ;
                    if (filedEntity.MaybeSpawnFlags.HasValue)
                    {
                        if (world.Has<HorizontalFlip>(liveEntity))
                        {
                            filedEntity.MaybeSpawnFlags |= FiledEntity.Flags.FlipX;
                        }
                        if (world.Has<VerticalFlip>(liveEntity))
                        {
                            filedEntity.MaybeSpawnFlags |= FiledEntity.Flags.FlipY;
                        }
                    }

                    // Set spawn info
                    if (liveLayer.LayerType != LevelLayerTypes.Prefabs)
                    {
                        var spawnInfo = new FiledEntity.SpawnInfo();
                        if (liveLayer.LayerType == LevelLayerTypes.TileSet)
                        {
                            spawnInfo.PosInVisualSet = world.Get<TileID>(liveEntity).PosInSet;
                        }
                        else if (liveLayer.LayerType == LevelLayerTypes.ImageSet)
                        {
                            // TODO!
                        }

                        filedEntity.MaybeSpawnInfo = spawnInfo;
                    }

                    // Set extra spawn info
                    if (world.Has<Editor_EntityOverrideExtraSpawnInfo>(liveEntity))
                    {
                        var extraSpawnInfo = new FiledEntity.ExtraSpawnInfo();
                        if (world.Has<Editor_EntityBaseColorBlend>(liveEntity))
                        {
                            extraSpawnInfo.ColorBlendOverride = world.Get<Editor_EntityBaseColorBlend>(liveEntity).Color.PackedValue();
                        }

                        var angle = world.Has<Angle>(liveEntity) ? world.Get<Angle>(liveEntity).Value : 0.0f;
                        if (world.Has<RotatesWithDirection>(liveEntity))
                        {
                            angle = MathUtilities.AngleFromUnitVector(world.Get<Direction2D>(liveEntity).Value);
                        }
                        if (angle != 0.0f)
                        {
                            extraSpawnInfo.AngleOverride = float.RadiansToDegrees(angle);
                        }

                        filedEntity.MaybeExtraSpawnInfo = extraSpawnInfo;
                    }

                    filedEntities.Add(filedEntity);
                }
                filedLayer.Entities = filedEntities.ToArray();
                filedLayers.Add(filedLayer);
            }
            filedRoom.Layers = filedLayers.ToArray();
            filedRooms.Add(filedRoom);
        }

        filedLevel.Rooms = filedRooms.ToArray();

        var json = JsonSerializer.Serialize(filedLevel, typeof(FiledLevel), JsonLevelContext);
        Directory.CreateDirectory(levelContentPath);
        var jsonOutputPath = Path.Combine(levelContentPath, Name + ".json");
        File.WriteAllText(jsonOutputPath, json);
    }
#endif
    
    public static LiveLevel LoadFromFile(string jsonPath, World world, PrefabManipulator prefabManipulator)
    {
#if DEBUG
        UndoRedo.ClearChangeHistoryList();
        UndoRedo.ClearRedoList();
#endif

        var filedLevel = (FiledLevel)JsonSerializer.Deserialize(
            File.ReadAllText(jsonPath),
            typeof(FiledLevel),
            JsonLevelContext
        );

        // 'SerializedVersion' can be used here, if needed

        var liveLevelResult = new LiveLevel();
        liveLevelResult.Name = filedLevel.Name;

        foreach (var filedRoom in filedLevel.Rooms)
        {
            var liveRoom = new Room(filedRoom, liveLevelResult); // adds itself to list in ctor

            foreach (var filedLayer in filedRoom.Layers)
            {
#if DEBUG
                var liveLayer = new EditorLayer(filedLayer, liveRoom); // adds itself to lists in ctor
#endif
                TileSetID? maybeTileSetID = null;

                var logSetNotFoundError = (string setName) => 
                    Logger.LogError($"Unable to load layer {filedLayer.EditorName}: Couldn't find visual set {setName}");

                var logVariantNotFoundError = (byte variantID) => 
                    Logger.LogError($"Unable to load layer {filedLayer.EditorName}: Couldn't find visual set variant with ID {variantID}");

                if (filedLayer.TypeID == LevelLayerTypes.TileSet)
                {
                    if (!TileSets.NameToTileSet.ContainsKey(filedLayer.MaybeVisualSet.Value.NameID))
                    {
                        logSetNotFoundError(filedLayer.MaybeVisualSet.Value.NameID);
                        continue;
                    }
                    var tileSet = TileSets.NameToTileSet[filedLayer.MaybeVisualSet.Value.NameID];

                    // Reminder that a variantID of 0 is valid; it means the default tileset.
                    if (tileSet.VariantTileSets.Count > filedLayer.MaybeVisualSet.Value.VariantID)
                    {
                        logVariantNotFoundError(filedLayer.MaybeVisualSet.Value.VariantID);
                    }
                    maybeTileSetID = tileSet.ID;
                }
                else if (filedLayer.TypeID == LevelLayerTypes.ImageSet)
                {
                    // FIXME: Implement!
                }

                for (int nthEntity = 0; nthEntity < filedLayer.Entities.Length; ++nthEntity)
                {
                    var filedEntity = filedLayer.Entities[nthEntity];
                    Prefabs prefabType;
                    PrefabSpawnInfo? maybeSpawnInfo = null;

                    if (filedLayer.TypeID == LevelLayerTypes.TileSet)
                    {
                        var tileID = new TileID(
                            filedEntity.MaybeSpawnInfo.Value.PosInVisualSet.Value, 
                            maybeTileSetID.Value, 
                            new TileSetVariantID(filedLayer.MaybeVisualSet.Value.VariantID)
                        );
                        var (prefabID, maybeFlags, maybeExtraData) = TileSet.GetTileMetadata(tileID);
                        prefabType = prefabID.ID;

                        maybeSpawnInfo = PrefabSpawnInfo.ForTile(tileID);
                        if (!filedEntity.MaybeSpawnFlags.HasValue)
                        {
                            filedEntity.MaybeSpawnFlags = maybeFlags;
                        }
                        if (filedEntity.MaybeExtraSpawnInfo == null)
                        {
                            filedEntity.MaybeExtraSpawnInfo = maybeExtraData;
                        }
                    }
                    /*else if (filedLayer.TypeID == LevelLayerTypes.ImageSet)
                    {
                        // FIXME: Implement!
                    }*/
                    else if (filedLayer.TypeID == LevelLayerTypes.Prefabs)
                    {
                        prefabType = filedLayer.MaybePrefabTypeForEntities.Value;
                    }
                    else
                    {
                        Logger.LogError($"Bad layer type: {filedLayer.TypeID}");
                        continue;
                    }

                    var spawnPosition = filedEntity.PositionRelativeToRoom + filedRoom.Position;

                    // Spawn the entities
                    var maybeLiveEntity = prefabManipulator.TrySpawnPrefab(
                        prefabType,
                        spawnPosition,
                        false,
                        maybeSpawnInfo,
                        filedEntity.MaybeSpawnFlags.HasValue ? filedEntity.MaybeSpawnFlags.Value : FiledEntity.Flags.None,
                        PrefabExtraSpawnInfo.FromFiled(filedEntity.MaybeExtraSpawnInfo) 
                    );

                    if (maybeLiveEntity.HasValue)
                    {
                        world.Set(maybeLiveEntity.Value, liveRoom.ID);

#if DEBUG                      
                        world.Set(maybeLiveEntity.Value, liveLayer.LayerID);
                        liveLayer.CachedEntities.Add(maybeLiveEntity.Value);
#endif

                        if (filedEntity.UniqueTag != null && filedEntity.UniqueTag.Length != 0)
                        {
                            world.Tag(maybeLiveEntity.Value, filedEntity.UniqueTag);
                        }
                    }
                }
            }
        }
        
        return liveLevelResult;
    }
}