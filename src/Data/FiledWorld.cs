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
[JsonSerializable(typeof(FiledLevel.Entity))]
[JsonSerializable(typeof(FiledLevel.Entity.ExtraDataTypes))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(uint))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Position2D))]
internal partial class FiledWorldContext : JsonSerializerContext
{
}

#if DEBUG
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
        }

        public struct FlatColorsTileSet
        {
            public string NameID;

            // Contains color hex codes, which we associate with the array index. 
            public int[] ColorDefinitions;
        }

        // No definition for regular TileSets here, since those are defined during source content processing.
    }
}
#endif

public enum LevelLayerTypes
{
    VisualTileSet = 0,
    SolidTileSet,
    ImageSet,
    SELECTABLE_IN_EDITOR_MAX,
    Prefabs,
    Unknown
}

public struct FiledLevel
{
    public int SerializedVersion;
    public string Name;
    public int Width;
    public int Height;
    public Room[] Rooms;

#if DEBUG
    // Contains the tile/image set information used by this level.
    public EditorDefinitions Definitions;
    public struct EditorDefinitions
    {
        public UsedVisualSet[] UsedVisualSets;

        // Just a reference to another tile/image set, with optional color variants for certain tiles/images.
        public struct UsedVisualSet
        {
            public string NameID;
            public ((int Image_X, int Image_Y), Color)[] ColoredVariants;
        }
    }
#endif

    public struct Room
    {
        public string Name;
        public Position2D Position; // top-left corner
        public int Width;
        public int Height;
        public Layer[] Layers;
    }

    public struct Layer
    {
        public LevelLayerTypes TypeID;
        public float Depth;
        public Entity[] Entities;

#if DEBUG
        public string Name;
        public Color ColorBlend;

        // TODO: Tileset/imageset name?
#endif
    }

    public struct Entity
    {
        [JsonPropertyName("Type")]
        public Prefabs PrefabID;

        [JsonPropertyName("Pos")]
        public Position2D StartPosition;

        [Flags]
        public enum Flags
        {
            FlipX = 1,
            FlipY = 2
        }

        [JsonPropertyName("Flags")]
        public Flags BitFlags;

        // NOTE: NEVER change the ordering here!!
        public enum ExtraDataTypes
        {
            ColorBlendOverride = 0,
            SpriteAnim,
            AngleOverride
        }

        [JsonPropertyName("Extra")]
        public Dictionary<ExtraDataTypes, object> ExtraDataList;
    }
}