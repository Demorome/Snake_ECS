#if DEBUG

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Data;
using RollAndCash.Relations;
using RollAndCash.Systems;

namespace RollAndCash.Editor;

public readonly record struct Editor_LevelLayerID(int ID);

// Represents a work-in-progress level while inside the editor.
public class LiveEditorLevel
{
    public string Name = "";
    private List<Layer> LayerIDs = new();
    public readonly Dictionary<string, Layer> Layers = new();

    static JsonSerializerOptions levelSerializerOptions = new JsonSerializerOptions
    {
        IncludeFields = true,
        WriteIndented = true
    };

    // FIXME: Create two files: one for the editor, one optimized for just the game.
    public void SaveToFile(string levelContentPath, World world)
    {
        var filedLevel = new FiledEditorLevel();
        filedLevel.Name = Name;

        List<FiledEditorLevel.Layer> filedLayers = new();
        foreach (var (_, layer) in Layers)
        {
            var filedLayer = new FiledEditorLevel.Layer();
            filedLayer.ColorBlend = layer.ColorBlend;
            filedLayer.Depth = layer.Depth;
            filedLayer.ImagesPerRow = layer.ImagesPerRow;
            filedLayer.Name = layer.Name;

            var filedImages = new List<(string SpriteAnimName, Color)>();
            foreach (var (spriteAnim, colorBlend) in layer.Images)
            {
                filedImages.Add(new(spriteAnim.SpriteAnimationInfo.Name, colorBlend));
            }
            filedLayer.Images = filedImages.ToArray();

            var filedEntities = new List<FiledEditorLevel.Layer.Entity>();
            foreach (var entity in layer.CachedEntities)
            {
                if (!world.Has<PrefabID>(entity))
                {
                    Logger.LogWarn($"{EditorSystem.EntityToString(world, entity)} in layer {layer.Name} couldn't be saved; it had no PrefabID.");
                    continue;
                }

                var filedEntity = new FiledEditorLevel.Layer.Entity();
                // FIXME: Get a "StartPosition" component instead?
                filedEntity.StartPosition = world.Get<Position2D>(entity);
                filedEntity.ColorBlend = world.Has<ColorBlend>(entity) ? world.Get<ColorBlend>(entity).Color : Color.White;
                filedEntity.PrefabID = world.Get<PrefabID>(entity).ID;
                // TODO: Set this to an empty string if we have the same sprite as the default prefab.
                filedEntity.SpriteAnimName = world.Get<SpriteAnimation>(entity).SpriteAnimationInfo.Name;
                filedEntity.Angle = world.Has<Angle>(entity) ? world.Get<Angle>(entity).Value : 0.0f;

                filedEntities.Add(filedEntity);
            }
            filedLayer.Entities = filedEntities.ToArray();

            filedLayers.Add(filedLayer);
        }

        filedLevel.Layers = filedLayers.ToArray();

        var json = JsonSerializer.Serialize(filedLevel, levelSerializerOptions);
        Directory.CreateDirectory(levelContentPath);
        var jsonOutputPath = Path.Combine(levelContentPath, Name + ".json");
        File.WriteAllText(jsonOutputPath, json);
    }

    public class Layer
    {
        public readonly Editor_LevelLayerID LayerID;
        public readonly LiveEditorLevel Level;

        public Layer(
            Types layerType,
            LiveEditorLevel level,
            string name = null,
            float depth = (float)DepthLayer.DefaultDepth
            )
        {
            LayerType = layerType;
            Level = level;
            Depth = depth;

            if (name == null)
            {
                name = LayerTypeToString(LayerType);
            }
            Level.ValidateLayerName(ref name);
            Name = name;

            lock (Level.Layers)
            {
                Level.Layers.Add(Name, this);
            }
            lock (Level.LayerIDs)
            {
                LayerID = new Editor_LevelLayerID(Level.LayerIDs.Count);
                Level.LayerIDs.Add(this);
            }
        }

        public string Name;
        public Types LayerType { get; private set; }
        public bool IsTiled => LayerType == Types.VisualTile || LayerType == Types.SolidTile;
        // Applies to all images.
        public Color ColorBlend { get; private set; } = Color.White;
        public List<(SpriteAnimation, Color)> Images { get; private set; } = new();
        public int ImagesPerRow = 8;
        public float PreviewScaleMult = 1;
        public float Depth { get; private set; } = (float)DepthLayer.DefaultDepth;
        public bool IsDepthLocked => LayerType == Types.SolidTile;
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

        public void ChangeLayerDepth(float newDepth, World world)
        {
            foreach (var entity in CachedEntities)
            {
                world.Set(entity, new Depth(newDepth));
            }
            Depth = newDepth;
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

        public enum Types
        {
            Prefab = 0,
            Image,
            VisualTile,
            SolidTile,
            SELECTABLE_COUNT,
            Unknown
        }

        public static string LayerTypeToString(Types layerType)
        {
            return layerType switch
            {
                Types.Image => "Image",
                Types.VisualTile => "Visual Tile",
                Types.SolidTile => "Solid Tile",
                Types.Prefab => "Prefab",
                Types.Unknown => "Unknown/Dynamic",
                _ => "Invalid level layer type!"
            };
        }
    }

    public void ValidateLayerName(ref string name)
    {
        if (!Layers.ContainsKey(name))
        {
            return;
        }

        name += " ";
        int i = 1;
        var testName = name + i.ToString();

        lock (Layers)
        {
            while (Layers.ContainsKey(testName))
            {
                ++i;
                testName = name + i.ToString();
            }
        }
        name = testName;
    }

    public void DeleteLayerCleanup(string layerToRemove, World world)
    {
        // FIXME: Undo support!
        // FIXME: If undone, need to re-apply relationship data too.
        // Ex: DebugEntiy DontDraw relation, if the layer was made invisible.

        // Deleting a layer deletes all entities in it.
        foreach (var entity in Layers[layerToRemove].CachedEntities)
        {
            world.Destroy(entity);
        }

        // Preserve the ID in the lookup, in case we want to undo this change.
        // May as well preserve the LevelLayer here too...?
        //IDLookup[LevelLayers[layerToRemove].LayerID.ID] = null;

        Layers.Remove(layerToRemove);
    }

    public Layer GetLayerFromID(Editor_LevelLayerID layerID)
    {
        return LayerIDs[layerID.ID];
    }
}



#endif