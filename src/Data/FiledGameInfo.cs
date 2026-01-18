using System.Collections.Generic;
using System.Numerics;
using MoonWorks.Graphics;
using RollAndCash.Components;
using System.Text.Json.Serialization;

namespace RollAndCash.Data;

/// <summary>
/// Contains info about the entire game.
/// </summary>
public struct FiledGameInfo
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
        public PrefabType? MaybePrefabTypeForEntities; // only used if layer type is Prefabs

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