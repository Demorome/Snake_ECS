#if DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Data;
using RollAndCash.Relations;
using RollAndCash.Systems;
using RollAndCash.Utility;

namespace RollAndCash.Editor;

// TODO: Optimize this by switching to discriminated unions, whenever C# supports those.

// This is to optimize JSON serializing w/ source generation: 
// https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation
[JsonSerializable(typeof(FiledEditorLevel))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(uint))]
internal partial class LevelContext : JsonSerializerContext
{
}

public readonly record struct Editor_LevelLayerID(int ID);

// Represents a work-in-progress level while inside the editor.
public class LiveEditorLevel
{
    public string Name = "";
    private List<Layer> LayerIDs = new();
    public readonly Dictionary<string, Layer> Layers = new();

    static JsonSerializerOptions LevelSerializerOptions = new JsonSerializerOptions
    {
        //IncludeFields = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static LevelContext JsonLevelContext = new(LevelSerializerOptions);

    // FIXME: Create two files: one for the editor, one optimized for just the game.
    public void SaveToFile(string levelContentPath, World world, PrefabManipulator prefabManipulator)
    {
        var filedLevel = new FiledEditorLevel();
        filedLevel.Name = Name;

        var AddExtraData = (ref FiledEditorLevel.Layer.Entity entity, FiledEntityExtraDataTypes type, object value) =>
        {
            if (entity.ExtraDataList == null)
            {
                entity.ExtraDataList = new();
            }
            entity.ExtraDataList.Add(type, value);
        };

        List<FiledEditorLevel.Layer> filedLayers = new();
        foreach (var (_, layer) in Layers)
        {
            var filedLayer = new FiledEditorLevel.Layer();
            filedLayer.ColorBlend = layer.ColorBlend;
            filedLayer.Depth = layer.Depth;
            filedLayer.ImagesPerRow = layer.ImagesPerRow;
            filedLayer.Name = layer.Name + " Layer";

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
                filedEntity.PrefabID = world.Get<PrefabID>(entity).ID;
                // FIXME: Get a "StartPosition" component instead?
                filedEntity.StartPosition = world.Get<Position2D>(entity);

                if (world.Has<ColorBlend>(entity))
                {
                    var colorBlend = world.Get<ColorBlend>(entity).Color;
                    if (!prefabManipulator.IsDefaultColorBlend(colorBlend, filedEntity.PrefabID))
                    {
                        AddExtraData(ref filedEntity,
                            FiledEntityExtraDataTypes.ColorBlend, colorBlend.PackedValue()
                        );
                    }
                }

                if (world.Has<SpriteAnimation>(entity))
                {
                    var spriteAnim = world.Get<SpriteAnimation>(entity);
                    if (!prefabManipulator.IsDefaultSprite(spriteAnim, filedEntity.PrefabID))
                    {
                        AddExtraData(ref filedEntity,
                            FiledEntityExtraDataTypes.SpriteAnim, spriteAnim.SpriteAnimationInfo.Name
                        );
                    }
                }

                var angle = world.Has<Angle>(entity) ? world.Get<Angle>(entity).Value : 0.0f;
                if (world.Has<RotatesWithDirection>(entity))
                {
                    angle = MathUtilities.AngleFromUnitVector(world.Get<Direction2D>(entity).Value);
                }
                if (angle != 0.0f)
                {
                    AddExtraData(ref filedEntity,
                        FiledEntityExtraDataTypes.Angle, float.RadiansToDegrees(angle)
                    );
                }

                filedEntities.Add(filedEntity);
            }
            filedLayer.Entities = filedEntities.ToArray();

            filedLayers.Add(filedLayer);
        }

        filedLevel.Layers = filedLayers.ToArray();

        var json = JsonSerializer.Serialize(filedLevel, typeof(FiledEditorLevel), JsonLevelContext);
        Directory.CreateDirectory(levelContentPath);
        var jsonOutputPath = Path.Combine(levelContentPath, Name + ".json");
        File.WriteAllText(jsonOutputPath, json);

        // DEBUG!!!!!!!!!!!!!!!!!!!!
        LoadFromFile(jsonOutputPath, world, prefabManipulator);
    }
    
    public static LiveEditorLevel LoadFromFile(string jsonPath, World world, PrefabManipulator prefabManipulator)
    {
        var filedLevel = (FiledEditorLevel)JsonSerializer.Deserialize(
            File.ReadAllText(jsonPath),
            typeof(FiledEditorLevel),
            JsonLevelContext
        );

        var result = new LiveEditorLevel();
        result.Name = filedLevel.Name;

        foreach (var filedLayer in filedLevel.Layers)
        {
            var liveLayer = new Layer(filedLayer, result); // adds itself to lists in the ctor

            foreach (var filedEntity in filedLayer.Entities)
            {
                // De-serialize ambiguously-typed values.
                if (filedEntity.ExtraDataList != null)
                {
                    foreach (var (type, value) in filedEntity.ExtraDataList)
                    {
                        switch (type)
                        {
                            case FiledEntityExtraDataTypes.Angle:
                                filedEntity.ExtraDataList[type] =
                                    ((JsonElement)value).Deserialize(typeof(float), JsonLevelContext);
                                break;
                            case FiledEntityExtraDataTypes.ColorBlend:
                                filedEntity.ExtraDataList[type] = Unsafe.BitCast<uint, Color>(
                                    (uint)(
                                        (JsonElement)value).Deserialize(typeof(uint), JsonLevelContext
                                    )
                                );
                                break;
                            case FiledEntityExtraDataTypes.SpriteAnim:
                                // FIXME: Optimize! Tiles probably don't need a unique SpriteAnimation, unless animated!
                                filedEntity.ExtraDataList[type] = new SpriteAnimation(
                                    SpriteAnimations.NameToInfoMap[
                                        (string)((JsonElement)value).Deserialize(typeof(string), JsonLevelContext)
                                    ]
                                );
                                break;
                            default:
                                Logger.LogError($"Unknown/unused extra data type: {type}");
                                break;
                        }
                    }
                }

                // TODO: Spawn the entities here!!!!
                var maybeLiveEntity = prefabManipulator.TrySpawnFiledPrefab(filedEntity);
                if (maybeLiveEntity.HasValue)
                {
                    world.Set(maybeLiveEntity.Value, liveLayer.LayerID);
                    liveLayer.CachedEntities.Add(maybeLiveEntity.Value);
                }
            }
        }

        return result;
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

        public Layer(
            FiledEditorLevel.Layer filedLayer,
            LiveEditorLevel level
            )
        {
            LayerType = filedLayer.TypeID;
            Level = level;
            Depth = filedLayer.Depth;
            Name = filedLayer.Name;
            ColorBlend = filedLayer.ColorBlend;

            foreach (var (spriteAnimName, color) in filedLayer.Images)
            {
                Images.Add(
                    (new SpriteAnimation(SpriteAnimations.NameToInfoMap[spriteAnimName]),
                        color
                    )
                );
            }

            ImagesPerRow = filedLayer.ImagesPerRow;
            //PreviewScaleMult = filedLayer.

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

        public Color MixLayerColorWithImageColor(Color imageColorBlend)
        {
            return Color.Lerp(ColorBlend, imageColorBlend, 0.5f);
        }

        public void ChangeLayerColorBlend(Color newColor, World world)
        {
            ColorBlend = newColor;

            // Recalculate the color blend for each entity in this layer.
            foreach (var entity in CachedEntities)
            {
                if (world.Has<Editor_LayerImageID>(entity))
                {
                    var tileSpriteIndex = world.Get<Editor_LayerImageID>(entity).ID;
                    var imageColorBlend = Images[tileSpriteIndex].Item2;
                    world.Set(entity, new ColorBlend(MixLayerColorWithImageColor(imageColorBlend)));
                }
                else
                {
                    // Just override the entity's colorblend.
                    // TODO: Could get fancier here, but we'd need to remember "original" colorblend.
                    world.Set(entity, new ColorBlend(newColor));
                }
            }
        }

        public void ChangeImageColorBlend(Editor_LayerImageID layerImageID, Color newColor, World world)
        {
            var (spriteID, oldTileColorBlend) = Images[layerImageID.ID];
            Images[layerImageID.ID] = (spriteID, newColor);

            // Recalculate the color blend for each entity in this layer that uses this image sprite.
            foreach (var entity in CachedEntities)
            {
                if (!world.Has<Editor_LayerImageID>(entity))
                {
                    Logger.LogError("Entity should have a Editor_LayerImageID component here!");
                    continue;
                }

                if (world.Get<Editor_LayerImageID>(entity).ID == layerImageID.ID)
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