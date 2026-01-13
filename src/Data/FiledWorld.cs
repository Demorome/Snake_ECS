using System.Collections.Generic;
using System.Numerics;
using MoonWorks.Graphics;
using RollAndCash.Components;
using System.Text.Json.Serialization;
using System;
using MoonTools.ECS;
using RollAndCash.Utility;
using MoonWorks;

namespace RollAndCash.Data;

// This is to optimize JSON serializing w/ source generation: 
// https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation
[JsonSerializable(typeof(FiledWorld))]
[JsonSerializable(typeof(FiledLevel))]
internal partial class FiledWorldContext : JsonSerializerContext
{
}

// Contains info about the entire game.
public struct FiledWorld
{
    // TODO: Could contain references to levels here, what the default level is, etc.

    public Defs Definitions;
    public struct Defs
    {
        public ImageSet[] ImageSets;
        public FlatColorsTileSet[] FlatColorsTileSets;

        public struct ImageSet
        {
            public string NameID;
            public int ImagesPerRow; // aka Width

            [JsonPropertyName("Sprites")]
            public string[] SpriteNames;

            // TODO: Allow for variants?
        }

        public struct FlatColorsTileSet
        {
            public string NameID;

            // Contains color hex codes, which we associate with the array index. 
            public int[] ColorDefinitions;
        }

        // No definition for regular TileSets here, since those are defined during source content processing.
        // Even TileSet variants are defined there, though the Editor may augment some JSON file to help.
        // This is because the TileSet must know each of its variants, so storing that info on its pre-existing JSON file is simplest.
    }
}

public enum LevelLayerTypes : byte
{
    TileSet = 0,
    ImageSet,
    VISUAL_SET_MAX = 19, // leave some space for new VisualSet types
    SELECTABLE_IN_EDITOR_MAX = VISUAL_SET_MAX,
    Prefabs,
    Unknown = 255
}

// FIXME: Make these extension funcs instead.
static class LevelLayerTypesFuncs
{
    public static bool IsVisualSet(LevelLayerTypes type)
    {
        return type < LevelLayerTypes.VISUAL_SET_MAX;
    }
#if DEBUG
    public static bool IsSelectableInEditor(LevelLayerTypes type)
    {
        return type < LevelLayerTypes.SELECTABLE_IN_EDITOR_MAX;
    }
#endif
}

public struct FiledLevel
{
    public int SerializedVersion;
    public string Name;
    public int Width;
    public int Height;
    public Room[] Rooms;

    // Contains the tile/image set information used by this level.
    // Mostly useless, but could be used for optimization purposes when rendering, perhaps?
    public UsedVisualSet[] UsedVisualSets;

#if DEBUG
    public EditorDefs EditorDefinitions;
    public struct EditorDefs
    {
    }
#endif

    public struct Room
    {
        public string Name;

        [JsonPropertyName("Pos")]
        public Position2D Position; // top-left corner

        public int W;
        public int H;
        public Layer[] Layers;
    }

    public struct Layer
    {
        public LevelLayerTypes TypeID;

        [JsonPropertyName("ColorBlend")]
        public Color? MaybeColorBlend;
        public float ColorBlendFactor;
        public float Depth;

        [JsonPropertyName("VisualSet")]
        public UsedVisualSet? MaybeVisualSet; // unused if layer type is Prefabs

        [JsonPropertyName("TypeForEntities")]
        public PrefabTypes? MaybePrefabTypeForEntities; // only used if layer type is Prefabs

        public FiledEntity[] Entities = [];

#if DEBUG
        public string? EditorName;
#endif

        public Layer()
        {
        }

#if DEBUG
        public Layer(LiveLevel.EditorLayer liveLayer)
        {
            TypeID = liveLayer.LayerType;
            MaybeColorBlend = liveLayer.MaybeColor;
            Depth = liveLayer.Depth;

            if (TypeID == LevelLayerTypes.TileSet)
            {
                var visualSet = new UsedVisualSet();
                visualSet.NameID = liveLayer.MaybeVisualSet!.Name;
                visualSet.VariantID = liveLayer.MaybeVisualSetVariantID!.Value.ID;
                MaybeVisualSet = visualSet;
            }
            else if (TypeID == LevelLayerTypes.ImageSet)
            {
                var visualSet = new UsedVisualSet();
                // FIXME: TODO!
                MaybeVisualSet = visualSet;
            }
            else if (TypeID == LevelLayerTypes.Prefabs)
            {
                MaybePrefabTypeForEntities = liveLayer.MaybePrefabType!.Value;
            }

            EditorName = liveLayer.Name;
        }
#endif
    }

    // Just a reference to a tile/image set.
    public struct UsedVisualSet
    {
        public string NameID;
        public byte VariantID;
    }
}

/// <summary>
/// Prefab type for a FiledEntity is determined by 
/// </summary>
public struct FiledEntity
{
    // Usually null, unless we wanted to name an entity in particular in the editor.
    // TODO: Could be made debug-only, but that might remove useful debugging info for release builds.
    [JsonPropertyName("Name")]
    public string UniqueTag;

    [JsonPropertyName("Pos")]
    public Position2D PositionRelativeToRoom;

    [JsonPropertyName("MaybeSpawnInfo")]
    public PrefabSpawnInfo_Filed? MaybeSpawnInfo;

    [Flags]
    public enum Flags
    {
        None    = 0,
        FlipX   = 1 << 0,
        FlipY   = 1 << 1,
    }

    // Null represents that we don't override 
    // the default spawn flags for the prefab.
    // Can't use 0 for that, since that represents overriding w/ 0.
    [JsonPropertyName("Flags")]
    public Flags? MaybeSpawnFlags;

    [JsonPropertyName("Overrides")]
    public PrefabSpawnInfoOverride? MaybeSpawnInfoOverrides;

    //MARK: Spawn Entity
    public Entity? ToLiveEntity(
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
        PrefabTypes prefabType;
        PrefabSpawnInfo_Processed spawnInfo = new();

        if (LevelLayerTypesFuncs.IsVisualSet(filedLayer.TypeID))
        {
            var visualFromSetID = new VisualFromSetID_ForSpawning(
                MaybeSpawnInfo!.Value.PosInVisualSet!.Value, 
                maybeTileSetID!.Value, 
                new VisualSetVariantID(filedLayer.MaybeVisualSet!.Value.VariantID)
            );

            var (prefabID, maybeSpawnFlags, maybeExtraSpawnInfo_FromVisualSet) 
                = VisualSet.GetMetadata(visualFromSetID)
            ;
            prefabType = prefabID.ID;

            spawnInfo = new PrefabSpawnInfo_Processed(visualFromSetID);
            if (!MaybeSpawnFlags.HasValue)
            {
                MaybeSpawnFlags = maybeSpawnFlags;
            }

            if (MaybeSpawnInfoOverrides == null)
            {
                MaybeSpawnInfoOverrides = maybeExtraSpawnInfo_FromVisualSet;
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

        var spawnPosition = PositionRelativeToRoom + filedRoom.Position;

        // Spawn the entities
        var maybeLiveEntity = prefabManipulator.TrySpawnPrefab(
            prefabType,
            spawnPosition,
            false,
            spawnInfo,
            MaybeSpawnFlags.HasValue ? MaybeSpawnFlags.Value : Flags.None,
            MaybeSpawnInfoOverrides
        );

        if (maybeLiveEntity.HasValue)
        {
            var liveEntity = maybeLiveEntity.Value;
            world.Set(liveEntity, liveRoom.ID);
            world.Set(liveEntity, new Depth(filedLayer.Depth));

            if (UniqueTag != null && UniqueTag.Length != 0)
            {
                world.Tag(liveEntity, UniqueTag);
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
    public static FiledEntity Editor_FromLiveEntity(
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
            world.Get<PrefabID>(liveEntity).ID,
            world.Get<Position2D>(liveEntity),
            false,
            spawnInfoForDummy
        )!.Value;
        
        // Set spawn flags
        {
            bool different = false;
            var spawnFlags = FiledEntity.Flags.None;

            if (CompareFlagComponent<HorizontalFlip>(liveEntity, dummyEntity, world, ref different))
            {
                spawnFlags |= FiledEntity.Flags.FlipX;
            }
            if (CompareFlagComponent<VerticalFlip>(liveEntity, dummyEntity, world, ref different))
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
    private static bool CompareFlagComponent<T>(
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