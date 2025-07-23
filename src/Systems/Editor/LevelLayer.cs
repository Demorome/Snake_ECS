#if DEBUG
using System.Collections.Generic;
using MoonTools.ECS;
using MoonWorks.Graphics;
using RollAndCash.Data;

namespace RollAndCash.Editor;

public class LevelLayer
{
    public static HashSet<string> LevelLayerNames = new();

    public static void ValidateLayerName(ref string name)
    {
        name += " ";
        int i = 1;
        var testName = name + i.ToString();

        lock (LevelLayerNames)
        {
            while (LevelLayerNames.Contains(testName))
            {
                ++i;
                testName = name + i.ToString();
            }
            LevelLayerNames.Add(testName);
        }
        name = testName;
    }

    public enum LevelLayerTypes
    {
        Image = 0,
        VisualTile,
        SolidTile,
        COUNT
    }
    public static string LayerTypeToString(LevelLayerTypes layerType)
    {
        return layerType switch
        {
            LevelLayerTypes.Image => "Image",
            LevelLayerTypes.VisualTile => "Visual Tile",
            LevelLayerTypes.SolidTile => "Solid Tile",
            _ => "Invalid level layer type"
        };
    }

    public LevelLayer(LevelLayerTypes layerType, string name = "New Layer")
    {
        LayerType = layerType;
        ValidateLayerName(ref name);
        Name = name;
    }

    public string Name;
    public LevelLayerTypes LayerType { get; private set; }
    public bool IsTiled => LayerType == LevelLayerTypes.VisualTile || LayerType == LevelLayerTypes.SolidTile;
    // Applies to all images.
    public Color ColorBlend = Color.White;
    public List<(SpriteAnimationInfoID, Color)> Images = new();
    public int ImagesPerRow = 8;
    public float Depth = -9999;
    public bool IsVisible = true;
    public List<Entity> CachedEntities = new();
}

#endif