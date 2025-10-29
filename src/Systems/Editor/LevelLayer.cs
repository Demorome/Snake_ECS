#if DEBUG
using System.Collections.Generic;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Data;
using RollAndCash.Relations;
using RollAndCash.Systems;

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
        Prefab = 0,
        Image,
        VisualTile,
        SolidTile,
        SELECTABLE_COUNT,
        Unknown
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
    public Color ColorBlend { get; private set; } = Color.White;
    public List<(SpriteAnimation, Color)> Images { get; private set; } = new();
    public int ImagesPerRow = 8;
    public float PreviewScaleMult = 1;
    public bool IsDepthLocked => LayerType == LevelLayerTypes.SolidTile;
    public bool IsVisible { get; private set; } = true;
    public List<Entity> CachedEntities = new();

    public void ReplaceImage(int tileSpriteID, SpriteAnimation newImage, World world)
    {
        Images[tileSpriteID] = (newImage, Color.White);

        // Update the image for every entity in this layer that was using the old one.
        foreach (var entity in CachedEntities)
        {
            if (!world.Has<Editor_LayerImageID>(entity))
            {
                Logger.LogError("Entity should have a Editor_TileSpriteIndex component here!");
                continue;
            }
            var entityTileSpriteID = world.Get<Editor_LayerImageID>(entity);
            if (tileSpriteID == entityTileSpriteID.ID)
            {
                world.Set(entity, newImage);
                world.Set(entity, new ColorBlend(MixLayerColorWithImageColor(Color.White)));
            }
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

    public Color MixLayerColorWithImageColor(Color tileColorBlend)
    {
        return Color.Lerp(ColorBlend, tileColorBlend, 0.5f);
    }

    public void ChangeLayerColorBlend(Color newColor, World world)
    {
        ColorBlend = newColor;

        // Recalculate the color blend for each entity in this layer.
        foreach (var entity in CachedEntities)
        {
            if (!world.Has<Editor_LayerImageID>(entity))
            {
                Logger.LogError("Entity should have a Editor_LayerImageID component here!");
                continue;
            }
            var tileSpriteIndex = world.Get<Editor_LayerImageID>(entity).ID;
            var tileColorBlend = Images[tileSpriteIndex].Item2;

            world.Set(entity, new ColorBlend(MixLayerColorWithImageColor(tileColorBlend)));
        }
    }

    public void ChangeTileColorBlend(Editor_LayerImageID tileSpriteID, Color newColor, World world)
    {
        var (spriteID, oldTileColorBlend) = Images[tileSpriteID.ID];
        Images[tileSpriteID.ID] = (spriteID, newColor);

        // Recalculate the color blend for each entity in this layer that uses this tile sprite.
        foreach (var entity in CachedEntities)
        {
            if (!world.Has<Editor_LayerImageID>(entity))
            {
                Logger.LogError("Entity should have a Editor_LayerImageID component here!");
                continue;
            }

            if (world.Get<Editor_LayerImageID>(entity).ID == tileSpriteID.ID)
            {
                world.Set(entity, new ColorBlend(MixLayerColorWithImageColor(newColor)));
            }
        }
    }

    // Assumes that newDepth isn't already being occupied as a level layer.
    public static void ChangeLayerDepth(
        float oldDepth,
        float newDepth,
        Dictionary<float, LevelLayer> levelLayers,
        World world
        )
    {
        foreach (var entity in levelLayers[oldDepth].CachedEntities)
        {
            world.Set(entity, new Depth(newDepth));
        }

        var prevLayerType = levelLayers[oldDepth].LayerType;
        var prevLayerName = levelLayers[oldDepth].Name;

        levelLayers.Remove(oldDepth);
        levelLayers.Add(newDepth, new LevelLayer(prevLayerType, prevLayerName));
    }

    public void DeleteLayerImage(int layerImageID, World world)
    {
        // FIXME: Undo support!
        for (int nthEntity = CachedEntities.Count - 1; nthEntity >= 0; --nthEntity)
        {
            var entity = CachedEntities[nthEntity];

            var otherLayerImageID = world.Get<Editor_LayerImageID>(entity).ID;
            if (otherLayerImageID == layerImageID)
            {
                world.Destroy(entity);
                CachedEntities.RemoveAt(nthEntity);
            }
            else if (otherLayerImageID > layerImageID)
            {
                world.Set(entity, new Editor_LayerImageID(otherLayerImageID - 1));
            }
        }
        Images.RemoveAt(layerImageID);
    }

    public static void DeleteLayerCleanup(float depthLayerToRemove,
        Dictionary<float, LevelLayer> levelLayers, World world)
    {
        // FIXME: Undo support!
        // FIXME: If undone, need to re-apply relationship data too.
        // Ex: DebugEntiy DontDraw relation, if the layer was made invisible.

        // Deleting a layer deletes all entities in it.
        foreach (var entity in levelLayers[depthLayerToRemove].CachedEntities)
        {
            world.Destroy(entity);
        }
        LevelLayerNames.Remove(levelLayers[depthLayerToRemove].Name);
        levelLayers.Remove(depthLayerToRemove);
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
}

#endif