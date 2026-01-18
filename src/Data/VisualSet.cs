using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json.Serialization;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.ComponentSerialization;
using RollAndCash.Content;

namespace RollAndCash.Data;

public readonly record struct PositionInVisualSet(ushort X, ushort Y);

// NOTE: These IDs are unique to each type of VisualSet!
public readonly record struct VisualSetID(ushort ID);

// An ID of 0 means using the default Visual Set.
public readonly record struct VisualSetVariantID(byte ID);

public readonly record struct VisualFromSetID_ForSpawning(
    PositionInVisualSet PosInSet, 
    VisualSetID VisualSetID, 
    VisualSetVariantID VariantID
)
{
    public static explicit operator VisualFromSetID_ForSpawning(TileID t)
    {
        return new VisualFromSetID_ForSpawning(t.PosInSet, t.TileSetID, t.VariantID);
    }
}

public abstract class VisualSet
{
    public static readonly List<VisualSet> IDLookup = new();

    //== Members
    public VisualSetID ID;

    public string Name;

    public ushort NumRows, NumColumns;

    // So that we can automatically assign a PrefabID, flags & extradata to certain visuals when we spawn them.
    // Visuals not contained here must automatically be purely visual with nothing special going on.
    public Dictionary<PositionInVisualSet, 
        (PrefabType, FiledEntity.Flags, PrefabSpawnInfoOverride?)> 
        Metadata = new();

    // The default VisualSet has a variant ID of 0, but isn't included here.
    // Thus, substract 1 whenever we index into this.
    public List<VisualSetVariant> VariantSets = new();

#if DEBUG
    public bool Editor_ShowGrid = true;
    public float Editor_PreviewScaleMult = 1f;

    public enum Editor_Types
    {
        Invalid = 0,
        FIRST,
        TileSet = FIRST,
        ImageSet,
        COUNT
    }
    public Editor_Types Editor_Type = Editor_Types.Invalid;
    public static Dictionary<Editor_Types, List<VisualSet>> 
        Editor_VisualSetsByType = new();

    static VisualSet()
    {
        for (Editor_Types i = Editor_Types.FIRST; i < Editor_Types.COUNT; ++i)
        {
            Editor_VisualSetsByType.Add(i, new List<VisualSet>());
        }
    }
#endif

    //== Constructors
    public VisualSet(string fileName)
    {
        Name = fileName;

        lock (IDLookup)
		{
			ID = new VisualSetID((ushort)IDLookup.Count);
			IDLookup.Add(this);
		}

#if DEBUG
        Editor_Type = Editor_Types.Invalid;
        if (this as TileSet != null)
        {
            Editor_Type = Editor_Types.TileSet;
        }

        if (Editor_Type == Editor_Types.Invalid)
        {
            Logger.LogError("Unrecognized VisualSet type!");
            return;
        }

        lock (Editor_VisualSetsByType)
        {
            Editor_VisualSetsByType[Editor_Type].Add(this);
        }
#endif
    }

    //== Methods
    public int NumVisuals => NumRows * NumColumns;
    public abstract Vector2 GetVisualSize(PositionInVisualSet posInVisualSet, VisualSetVariantID variantID);
    public Color GetVisualColor(PositionInVisualSet posInVisualSet, VisualSetVariantID variantID)
    {
        if (variantID.ID == 0)
        {
            return Color.White;
        }
        var variantTileSet = VariantSets[variantID.ID];
        return variantTileSet.GetTileColorOverride(posInVisualSet);
    }
    public bool CanSetVisualColor(VisualSetVariantID variantID)
    {
        // Enforce that the default VisualSet variant can't be changed.
        return variantID.ID != 0;
    }
    public byte GetVariantCount() => (byte)VariantSets.Count;

    public static (PrefabType, FiledEntity.Flags, PrefabSpawnInfoOverride?) 
        GetMetadata(VisualFromSetID_ForSpawning v)
    {
        return GetMetadata(v.PosInSet, v.VisualSetID, v.VariantID);
    }

    public static (PrefabType, FiledEntity.Flags, PrefabSpawnInfoOverride?) 
        GetMetadata(
            PositionInVisualSet PosInSet, 
            VisualSetID TileSetID, 
            VisualSetVariantID VariantID
            )
    {
        var tileSet = IDLookup[TileSetID.ID];

        (PrefabType, FiledEntity.Flags, PrefabSpawnInfoOverride?) result;
        if (tileSet.Metadata.ContainsKey(PosInSet))
        {
            result = tileSet.Metadata[PosInSet];
        }
        else
        {
            result = (
                PrefabType.VisualTile, 
                FiledEntity.Flags.None, 
                null
            );
        }

        if (VariantID.ID != 0)
        {
            var variantTileSet = tileSet.VariantSets[VariantID.ID];
            var maybeMetadataOverride = variantTileSet.GetTileTileMetadataOverride(PosInSet);
            if (maybeMetadataOverride.HasValue)
            {
                if (maybeMetadataOverride.Value.Item1.HasValue)
                {
                    result.Item1 = maybeMetadataOverride.Value.Item1.Value;
                }
                if (maybeMetadataOverride.Value.Item2.HasValue)
                {
                    result.Item2 = maybeMetadataOverride.Value.Item2.Value;
                }
                if (maybeMetadataOverride.Value.Item3 != null)
                {
                    result.Item3 = maybeMetadataOverride.Value.Item3;
                }
            }
        }
        return result;
    }

#if DEBUG
    // NOT to be used when loading entities from files, since those might have unique changes.
    public static Entity? Editor_TryCreateEntityFromVisualSet(
        VisualFromSetID_ForSpawning visualFromSetID,
        Position2D spawnPosition,
        World world,
        PrefabManipulator prefabManipulator,
        LiveLevel.Room? maybeRoom = null,
        LiveLevel.EditorLayer? maybeLayer = null,
        bool isDummyVisual = false,
        bool isDummyForPaintingPreview = false
    )
    {
        var (prefabType, maybeSpawnFlags, maybeExtraSpawnInfo_FromVisualSet) 
            = GetMetadata(visualFromSetID);

        if (isDummyVisual)
        {
            if (EntityPrefabs.IsTile(prefabType))
            {
                prefabType = PrefabType.VisualTile;
            }

            if (!EntityPrefabs.IsPurelyVisual(prefabType))
            {
                Logger.LogError($"Unrecognized prefab type for dummy visual: {prefabType}");
                return null;
            }
        }
        else
        {
            if (maybeRoom == null || maybeLayer == null)
            {
                Logger.LogError("Room/layer shouldn't be null for a non-dummy visual!");
                return null;
            }
        }

        var maybeSpawnInfo = new PrefabSpawnInfo_Processed(visualFromSetID);

        // Spawn the entities
        var maybeNewEntity = prefabManipulator.TrySpawnPrefab(
            prefabType,
            spawnPosition,
            false,
            maybeSpawnInfo,
            maybeSpawnFlags,
            maybeExtraSpawnInfo_FromVisualSet
        );
        if (!maybeNewEntity.HasValue)
        {
            return null;
        }
        var newEntity = maybeNewEntity.Value;

        if (isDummyVisual)
        {
            if (isDummyForPaintingPreview)
            {
                world.Set(newEntity, new Editor_DummyVisualFromVisualSet_ForPaintingPreview());
            }
            else
            {
                world.Set(newEntity, new Editor_DummyVisualFromVisualSet_ForVisualSet());
            }
            world.Set(newEntity, new Editor_DontShowInLists());
            world.Set(newEntity, new Editor_DontAddToLevel());
        }
        else
        {
            world.Set(newEntity, maybeRoom!.ID);
            world.Set(newEntity, maybeLayer!.LayerID);
            maybeLayer.CachedEntities.Add(newEntity);
        }

        if (maybeLayer != null)
        {
            world.Set(newEntity, new Depth(maybeLayer.Depth));
        }

        return newEntity;
    }

    public abstract bool Editor_IsVisualFullyTransparent(
        PositionInVisualSet posInVisualSet, 
        VisualSetVariantID variantID
    );
    public abstract bool Editor_CanAddOrRemoveVisuals();
    public bool Editor_CanResizeColumnCount => Editor_CanAddOrRemoveVisuals();
    public bool Editor_CanDeleteOrCreate => Editor_CanAddOrRemoveVisuals();

    /// <summary>
    /// Returns false if we can't resize
    /// </summary>
    public abstract bool Editor_TrySetColumnCount(ushort newWidth); 
    
    public bool Editor_TrySetVisualColor(
        PositionInVisualSet posInVisualSet, 
        VisualSetVariantID variantID, 
        Color newColor
        )
    {
        if (!CanSetVisualColor(variantID))
        {
            return false;
        }
        var variantTileSet = VariantSets[variantID.ID];
        variantTileSet.Editor_SetTileColorOverride(posInVisualSet, newColor);

        // Entities will have their appearance changed elsewhere, in the menu code.

        return true;
    }

    public Editor_Types FromLayerType(LevelLayerTypes levelLayerType)
    {
        // TODO
        throw new NotImplementedException();
    }
    public string Editor_VisualName(bool plural = false)
    {
        string result = "";
        switch (Editor_Type)
        {
            case Editor_Types.TileSet:
                result = "Tile";
                break;
            case Editor_Types.ImageSet:
                result = "Image";
                break;
            default:
                Logger.LogError("Unknown type for VisualSet!");
                return "ERROR";
        }
        if (plural)
        {
            result += "s";
        }
        return result;
    }


#endif
}

public class VisualSetVariant
{
    public VisualSetVariantID ID;
    public VisualSet Parent;

#if DEBUG
    // Only used as an optional describer for the Editor.
    public string? Editor_Name;
#endif

    // NOTE: If any field is non-null, it completely overrides the base field of the TileSet.
    public Dictionary<PositionInVisualSet, 
        (PrefabType?, FiledEntity.Flags?, PrefabSpawnInfoOverride?)>  
        TileMetadataOverrides = new();

    public VisualSetVariant(VisualSet parent)
    {
        Parent = parent;
        lock (Parent.VariantSets)
        {
            ID = new VisualSetVariantID((byte)(Parent.VariantSets.Count + 1));
            Parent.VariantSets.Add(this);
        }
    }

    public Color GetTileColorOverride(PositionInVisualSet tilePosInSet)
    {
        if (TileMetadataOverrides.ContainsKey(tilePosInSet))
        {
            var (_, _, maybeSpawnInfoOverrides) = TileMetadataOverrides[tilePosInSet];
            if (maybeSpawnInfoOverrides.HasValue)
            {
                // NOTE: It's pointless that we store the full ColorBlend
                // struct here, since there can only be a base, unmodified color
                // here. However, it means we don't need to handle an entirely 
                // different struct.
                if (maybeSpawnInfoOverrides.Value.ColorBlend.HasValue)
                {
                    return maybeSpawnInfoOverrides.Value.ColorBlend.Value.Color;
                }
            }
        }
        return Color.White;
    }

#if DEBUG
    public void Editor_SetTileColorOverride(
        PositionInVisualSet tilePosInSet, Color newColor)
    {
        if (TileMetadataOverrides.ContainsKey(tilePosInSet))
        {
            var (maybePrefabID, maybeFlags, maybeExtraSpawnInfo) =
                TileMetadataOverrides[tilePosInSet];

            PrefabSpawnInfoOverride newExtraSpawnInfo;
            if (maybeExtraSpawnInfo.HasValue)
            {
                newExtraSpawnInfo = maybeExtraSpawnInfo.Value;
            }
            else
            {
                newExtraSpawnInfo = new();
            }
            newExtraSpawnInfo.ColorBlend = new ColorBlend(newColor);

            TileMetadataOverrides[tilePosInSet] =
                (maybePrefabID, maybeFlags, newExtraSpawnInfo);
        }
    }
#endif

    public (PrefabType?, FiledEntity.Flags?, PrefabSpawnInfoOverride?)? 
        GetTileTileMetadataOverride(PositionInVisualSet tilePosInSet)
    {
        if (TileMetadataOverrides.ContainsKey(tilePosInSet))
        {
            return TileMetadataOverrides[tilePosInSet];
        }
        return null;
    }
}