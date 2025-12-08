using System.Collections.Generic;
using System.Numerics;
using MoonWorks.Graphics;
using RollAndCash.Components;
using System.Text.Json.Serialization;
using System;

namespace RollAndCash.Data;

// This is to optimize JSON serializing w/ source generation: 
// https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation
// TODO: Not sure how necessary each of these are beyond the first.
[JsonSerializable(typeof(FiledWorld))]
[JsonSerializable(typeof(FiledLevel))]
[JsonSerializable(typeof(FiledLevel.Layer))]
[JsonSerializable(typeof(FiledLevel.Room))]
[JsonSerializable(typeof(FiledEntity))]
[JsonSerializable(typeof(FiledEntity.ExtraSpawnInfo))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(uint))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Position2D))]
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

public enum LevelLayerTypes
{
    TileSet = 0,
    ImageSet,
    VISUAL_SET_MAX,
    SELECTABLE_IN_EDITOR_MAX = VISUAL_SET_MAX,
    Prefabs,
    Unknown
}

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
        public Color Color;
        public float Depth;

        public UsedVisualSet? MaybeVisualSet; // unused if layer type is Prefabs

        [JsonPropertyName("TypeForEntities")]
        public Prefabs? MaybePrefabTypeForEntities; // only used if layer type is Prefabs

        public FiledEntity[] Entities;

#if DEBUG
        public string EditorName;
#endif

        public Layer()
        {
        }

#if DEBUG
        public Layer(LiveLevel.EditorLayer liveLayer)
        {
            TypeID = liveLayer.LayerType;
            Color = liveLayer.Color;
            Depth = liveLayer.Depth;

            if (TypeID == LevelLayerTypes.TileSet)
            {
                var visualSet = new UsedVisualSet();
                visualSet.NameID = liveLayer.MaybeVisualSet.Name;
                visualSet.VariantID = liveLayer.MaybeVisualSetVariantID.Value.ID;
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
                MaybePrefabTypeForEntities = liveLayer.MaybePrefabType.Value;
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

public struct FiledEntity
{
    // Usually null, unless we wanted to name an entity in particular in the editor.
    [JsonPropertyName("Name")]
    public string UniqueTag;

    // Some prefabs need args to be spawned.
    // TODO: Damn you C# for not having Discriminated Unions yet!!!
    public struct SpawnInfo
    {
        public PositionInVisualSet? PosInVisualSet;

        [JsonPropertyName("SpriteAnim")]
        public string SpriteAnimName;
    }
    public SpawnInfo? MaybeSpawnInfo;

    // Relative to the Room's position.
    [JsonPropertyName("Pos")]
    public Position2D PositionRelativeToRoom;

    [Flags]
    public enum Flags
    {
        None    = 0,
        FlipX   = 1 << 0,
        FlipY   = 1 << 1,
    }

    [JsonPropertyName("Flags")]
    public Flags? MaybeSpawnFlags;

    // To save unique editor changes to an entity, like changing its color blend.
    public struct ExtraSpawnInfo
    {
        public uint? ColorBlendOverride;
        public float? AngleOverride;
    }

    [JsonPropertyName("Extra")]
    public ExtraSpawnInfo? MaybeExtraSpawnInfo;
}