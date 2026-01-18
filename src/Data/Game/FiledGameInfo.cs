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