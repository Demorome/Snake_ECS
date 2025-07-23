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
        Image = 0,
        VisualTile,
        SolidTile,
        COUNT
    }

    public LevelLayer(LevelLayerTypes layerType, string name = "New Layer")
    {
        LayerType = layerType;
        ValidateLayerName(ref name);
        Name = name;

        if (layerType == LevelLayerTypes.SolidTile)
        {
            Depth = (float)DepthLayer.Tile_Solid;
        }
    }

    public string Name;
    public LevelLayerTypes LayerType { get; private set; }
    public bool IsTiled => LayerType == LevelLayerTypes.VisualTile || LayerType == LevelLayerTypes.SolidTile;
    // Applies to all images.
    public Color ColorBlend { get; private set; } = Color.White;
    public List<(SpriteAnimation, Color)> Images { get; private set; } = new();
    public int ImagesPerRow = 8;
    public float Depth { get; private set; } = -2;
    public bool IsDepthLocked => LayerType == LevelLayerTypes.SolidTile;
    public bool IsVisible { get; private set; } = true;
    public List<Entity> CachedEntities = new();

    public void ReplaceImage(int tileSpriteID, SpriteAnimation newImage, World world)
    {
        Images[tileSpriteID] = (newImage, Color.White);

        // Update the image for every entity in this layer that was using the old one.
        foreach (var entity in CachedEntities)
        {
            if (!world.Has<Editor_TileSpriteID>(entity))
            {
                Logger.LogError("Entity should have a Editor_TileSpriteIndex component here!");
                continue;
            }
            var entityTileSpriteID = world.Get<Editor_TileSpriteID>(entity);
            if (tileSpriteID == entityTileSpriteID.ID)
            {
                world.Set(entity, newImage);
                world.Set(entity, new ColorBlend(MixLayerColorWithTileColor(Color.White)));
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

    public Color MixLayerColorWithTileColor(Color tileColorBlend)
    {
        return Color.Lerp(ColorBlend, tileColorBlend, 0.5f);
    }

    public void ChangeLayerColorBlend(Color newColor, World world)
    {
        ColorBlend = newColor;

        // Recalculate the color blend for each entity in this layer.
        foreach (var entity in CachedEntities)
        {
            if (!world.Has<Editor_TileSpriteID>(entity))
            {
                Logger.LogError("Entity should have a Editor_TileSpriteIndex component here!");
                continue;
            }
            var tileSpriteIndex = world.Get<Editor_TileSpriteID>(entity).ID;
            var tileColorBlend = Images[tileSpriteIndex].Item2;

            world.Set(entity, new ColorBlend(MixLayerColorWithTileColor(tileColorBlend)));
        }
    }

    public void ChangeTileColorBlend(Editor_TileSpriteID tileSpriteID, Color newColor, World world)
    {
        var (spriteID, oldTileColorBlend) = Images[tileSpriteID.ID];
        Images[tileSpriteID.ID] = (spriteID, newColor);

        // Recalculate the color blend for each entity in this layer that uses this tile sprite.
        foreach (var entity in CachedEntities)
        {
            if (!world.Has<Editor_TileSpriteID>(entity))
            {
                Logger.LogError("Entity should have a Editor_TileSpriteIndex component here!");
                continue;
            }

            if (world.Get<Editor_TileSpriteID>(entity).ID == tileSpriteID.ID)
            {
                world.Set(entity, new ColorBlend(MixLayerColorWithTileColor(newColor)));
            }
        }
    }

    public void ChangeLayerDepth(float newDepth, World world)
    {
        Depth = newDepth;
        foreach (var entity in CachedEntities)
        {
            world.Set(entity, new Depth(newDepth));
        }
    }

    public static void DeleteLayerCleanup(Editor_LevelLayerID levelLayerToRemove,
        List<LevelLayer> levelLayers, World world)
    {
        // FIXME: Undo support!
        // FIXME: If undone, need to re-apply relationship data too.
        // Ex: DebugEntiy DontDraw relation, if the layer was made invisible.

        // Deleting a layer deletes all entities in it.
        foreach (var entity in levelLayers[levelLayerToRemove.ID].CachedEntities)
        {
            world.Destroy(entity);
        }
        LevelLayerNames.Remove(levelLayers[levelLayerToRemove.ID].Name);
        levelLayers.RemoveAt(levelLayerToRemove.ID);

        // Update Editor_LevelLayerID components for entities in other layers, if they had a greater ID.
        for (int i = 0; i < levelLayers.Count; ++i)
        {
            foreach (var entity in levelLayers[i].CachedEntities)
            {
                var entityLevelLayer = world.Get<Editor_LevelLayerID>(entity);
                if (entityLevelLayer.ID > levelLayerToRemove.ID)
                {
                    world.Set(entity, new Editor_LevelLayerID(entityLevelLayer.ID - 1));
                }
            }
        }
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