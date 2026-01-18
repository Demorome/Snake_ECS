using MoonWorks.Graphics;
using RollAndCash.Components;
using System.Text.Json.Serialization;

namespace RollAndCash.Data;

public struct FiledLevel
{
    public int SerializedVersion;
    public string Name;
    public int Width;
    public int Height;
    public Room[] Rooms;

    // Contains the tile/image set information used by this level.
    // Mostly useless, but could be used for optimization purposes 
    // when rendering, perhaps?
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
        public Layer(LoadedLevel.EditorLayer liveLayer)
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