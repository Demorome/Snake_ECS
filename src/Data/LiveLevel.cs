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


/// <summary>
/// Each level has multiple rooms. <br/>
/// 
/// These rooms are self-contained gameplay areas, 
/// with no direct spatial reference to other rooms. <br/>
/// 
/// In-editor, rooms connect together w/ teleport prefabs w/ a RoomID. 
/// Rooms are laid out in diagram style, 
/// to show how they're all tied together. <br/>
/// 
/// A room may connect to multiple rooms in a single corner, ala Metroid. 
/// These rooms may not be directly connected; 
/// they can have gaps for clarity in the editor. <br/>
/// 
/// Only a single room will be loaded/active at any given time. <br/>
/// 
/// Outside the editor, entities don't need to know what room they're in,
/// since they can only ever exist in the current room.
/// </summary>
public readonly record struct LevelRoomID(int ID);

#if DEBUG
/// <summary>
/// Each room has multiple layers, 
/// which are a group of entities and tiles for the editor. 
/// </summary>
public readonly record struct Editor_LevelLayerID(int ID);
#endif

/// <summary>
/// Represents a level that's fully loaded in-game.
/// </summary>
public class LiveLevel
{
    public string Name = "";
    public readonly List<Room> Rooms = new();

    public Room GetRoomFromID(LevelRoomID roomID)
    {
        return Rooms[roomID.ID];
    }

    public class Room
    {
        public LevelRoomID ID;
        public readonly LiveLevel Level;
        public string Name;
        public Position2D Position; // top-left corner
        public int Width = Dimensions.GAME_W;
        public int Height = Dimensions.GAME_H;

#if DEBUG
        public readonly Dictionary<string, EditorLayer> LayersByName;
        public readonly List<EditorLayer> Layers;
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
            LayersByName = new();
            Layers = new();
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
            LayersByName = new(filedRoom.Layers.Length);
            Layers = new(filedRoom.Layers.Length);
#endif
        }

#if DEBUG
        public EditorLayer GetLayerFromID(Editor_LevelLayerID layerID)
        {
            return Layers[layerID.ID];
        }

        public void ValidateLayerName(ref string name)
        {
            if (!LayersByName.ContainsKey(name))
            {
                return;
            }

            name += " ";
            int i = 2;
            var testName = name + i.ToString();

            lock (LayersByName)
            {
                while (LayersByName.ContainsKey(testName))
                {
                    ++i;
                    testName = name + i.ToString();
                }
            }
            name = testName;
        }

        public void DeleteLayerCleanup(ref LiveLevel.EditorLayer layerToRemove, World world)
        {
            // FIXME: Undo support!
            // FIXME: If undone, need to re-apply relationship data too.
            // Ex: DebugEntiy DontDraw relation, if the layer was made invisible.

            // Deleting a layer deletes all entities in it.
            foreach (var entity in layerToRemove.CachedEntities)
            {
                world.Destroy(entity);
            }

            // Preserve the ID in the lookup, in case we want to undo this change.
            // May as well preserve the Layer here too...?
            //Layers[LayerID.ID] = null;

            LayersByName.Remove(layerToRemove.Name);
            layerToRemove = null;
        }
#endif
    }

#if DEBUG
    public class EditorLayer
    {
        public string Name { get; private set; }
        public bool IsNameLocked => LayerType == LevelLayerTypes.Prefabs;
        public bool TrySetName(string newName)
        {
            if (!IsNameLocked)
            {
                Name = newName;
            }
            return IsNameLocked;
        }
        public readonly Editor_LevelLayerID LayerID;
        public readonly Room Room;
        public LiveLevel Level => Room.Level;
        public LevelLayerTypes LayerType { get; private set; }
        public bool IsTiled => LayerType == LevelLayerTypes.TileSet;

        // To spawn stuff, a layer either uses either a:
        // a) visual set, or 
        // b) prefab type.
        // The visual set is expected to contain prefab types.
        // FIXME: Use discriminated union here when it's available!!
        public VisualSet MaybeVisualSet;
        public VisualSetVariantID? MaybeVisualSetVariantID;
        public PrefabTypes? MaybePrefabType;

        /// <summary>
        /// Applies a tint to all images/tiles in this layer.
        /// Could've set it to White by default instead of having an off toggle,
        /// but this way we could support custom blend modes 
        /// more easily later on.
        /// </summary>
        public Toggleable<Color> MaybeColor { get; private set; } = default;
        public float ColorBlendFactor { get; private set; } = 0.5f;

        public float Depth { get; private set; } = (float)DepthLayer.PlaceholderDepth;
        public bool IsDepthLocked => LayerType == LevelLayerTypes.Prefabs;
        public bool IsVisible { get; private set; } = true;
        public List<Entity> CachedEntities = new();

        public EditorLayer(
            LevelLayerTypes layerType,
            LiveLevel.Room room,
            string name = null,
            float depth = (float)DepthLayer.PlaceholderDepth
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

            lock (room.LayersByName)
            {
                room.LayersByName.Add(Name, this);
            }
            lock (room.Layers)
            {
                LayerID = new Editor_LevelLayerID(room.Layers.Count);
                room.Layers.Add(this);
            }
        }

        public EditorLayer(FiledLevel.Layer filedLayer, Room room)
        {
            LayerType = filedLayer.TypeID;
            Room = room;
            Depth = filedLayer.Depth;
            // NOTE: Assumes the layer's name's uniqueness was preserved.
            Name = filedLayer.EditorName;
            MaybeColor = filedLayer.MaybeColorBlend;
            ColorBlendFactor = filedLayer.ColorBlendFactor;

            lock (room.LayersByName)
            {
                room.LayersByName.Add(Name, this);
            }
            lock (room.Layers)
            {
                LayerID = new Editor_LevelLayerID(room.Layers.Count);
                room.Layers.Add(this);
            }
        }

        // Should never be called if this isn't a TileSet-type layer.
        /*public void ReplaceTileSet(TileSet newTileSet, World world)
        {
            MaybeVisualSet = newTileSet;

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
        }*/

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

        private Color MixLayerColorWithEntityColor(
            Color layerColor, Color entityBaseColor)
        {
            return Color.Lerp(layerColor, entityBaseColor, ColorBlendFactor);
        }

        private void UpdateColorsForEntitiesInLayer(World world)
        {
            // Recalculate the color blend for each entity in this layer.
            foreach (var entity in CachedEntities)
            {
                Color? maybeEntityBaseColor = null;
                if (world.Has<ColorBlend>(entity))
                {
                    maybeEntityBaseColor = 
                        world.Get<ColorBlend>(entity).Editor_MaybeBaseColor;
                }
                if (!this.MaybeColor.HasValue)
                {
                    if (!maybeEntityBaseColor.HasValue)
                    {
                        world.Remove<ColorBlend>(entity);
                    }
                    else
                    {
                        // Entity's color is unnaffected by the layer.
                        world.Set(entity, 
                            new ColorBlend(
                                maybeEntityBaseColor.Value, 
                                maybeEntityBaseColor.Value
                            )
                        );
                    }
                }
                else if (maybeEntityBaseColor.HasValue)
                {
                    world.Set(entity, 
                        new ColorBlend(
                            MixLayerColorWithEntityColor(
                                this.MaybeColor.Value, maybeEntityBaseColor.Value
                            ), 
                            maybeEntityBaseColor.Value
                        )
                    );
                }
                else
                {
                    // Entity's color tint will be entirely based on the layer.
                    world.Set(entity, 
                        new ColorBlend(
                            MixLayerColorWithEntityColor(
                                this.MaybeColor.Value, Color.White
                            ),
                            null // entity has no baseColor of its own.
                        )
                    );
                }
            }
        }
        public void ChangeLayerColorBlend(Toggleable<Color> newColorMaybeOff, World world)
        {
            this.MaybeColor = newColorMaybeOff;
            UpdateColorsForEntitiesInLayer(world);
        }
        public void ChangeLayerColorBlendFactor(float newFactor, World world)
        {
            this.ColorBlendFactor = newFactor;
            UpdateColorsForEntitiesInLayer(world);
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
    public void SaveToFile(
        string levelContentPath, 
        World world, 
        PrefabManipulator prefabManipulator
        )
    {
        var filedLevel = new FiledLevel();
        filedLevel.SerializedVersion = 1;
        filedLevel.Name = Name;

        List<FiledLevel.Room> filedRooms = new(Rooms.Count);
        foreach (var liveRoom in Rooms)
        {
            var filedRoom = new FiledLevel.Room();

            List<FiledLevel.Layer> filedLayers = new(liveRoom.LayersByName.Count);
            foreach (var (_, liveLayer) in liveRoom.LayersByName)
            {
                var filedLayer = new FiledLevel.Layer(liveLayer);

                var filedEntities = new List<FiledEntity>(liveLayer.CachedEntities.Count);
                foreach (var liveEntity in liveLayer.CachedEntities)
                {
                    if (!world.Has<PrefabID>(liveEntity))
                    {
                        Logger.LogError($"{EditorSystem.EntityToString(world, liveEntity)} in layer {liveLayer.Name} has no PrefabID. We won't save it!");
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
                if (liveRoom.LayersByName.ContainsKey(filedLayer.EditorName))
                {
                    Logger.LogError($"Unable to load layer {filedLayer.EditorName}: Name is no longer unique w/ other layers in this room!");
                    continue;
                }
                var liveEditorLayer = new EditorLayer(filedLayer, liveRoom); // adds itself to lists in ctor
#endif
                VisualSetID? maybeTileSetID = null;

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
                    if (tileSet.VariantSets.Count > filedLayer.MaybeVisualSet.Value.VariantID)
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