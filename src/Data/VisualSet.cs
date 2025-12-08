using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Content;

namespace RollAndCash.Data;

public readonly record struct PositionInVisualSet(ushort X, ushort Y);

// NOTE: These IDs are unique to each type of VisualSet!
public readonly record struct VisualSetID(ushort ID);

// An ID of 0 means using the default Visual Set.
public readonly record struct VisualSetVariantID(byte ID);

public readonly record struct VisualFromSetID_ForSpawning(PositionInVisualSet PosInSet, VisualSetID VisualSetID, VisualSetVariantID VariantID);

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
        (PrefabID, FiledEntity.Flags, PrefabExtraSpawnInfo?)> 
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
        TileSet,
        ImageSet
    }
    public Editor_Types Editor_Type = Editor_Types.Invalid;
    public static Dictionary<Editor_Types, VisualSet> Editor_VisualSetsByType = new();
#endif

    //== Constructors
    public VisualSet(string name)
    {
        Name = name;

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
            Editor_VisualSetsByType.Add(Editor_Type, this);
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
        return variantID.ID != 0;
    }
    public byte GetVariantCount() => (byte)VariantSets.Count;

    public static (PrefabID, FiledEntity.Flags, PrefabExtraSpawnInfo?) 
        GetMetadata(VisualFromSetID_ForSpawning v)
    {
        return GetMetadata(v.PosInSet, v.VisualSetID, v.VariantID);
    }

    public static (PrefabID, FiledEntity.Flags, PrefabExtraSpawnInfo?) 
        GetMetadata(
            PositionInVisualSet PosInSet, 
            VisualSetID TileSetID, 
            VisualSetVariantID VariantID
            )
    {
        var tileSet = IDLookup[TileSetID.ID];

        (PrefabID, FiledEntity.Flags, PrefabExtraSpawnInfo?) result;
        if (tileSet.Metadata.ContainsKey(PosInSet))
        {
            result = tileSet.Metadata[PosInSet];
        }
        else
        {
            result = (new PrefabID(Prefabs.VisualTile), FiledEntity.Flags.None, null);
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
        LiveLevel.Room currentRoom,
        LiveLevel.EditorLayer maybeLayer = null,
        bool isDummyVisual = false,
        bool isDummyForPaintingPreview = false
    )
    {
        (var prefabID, var maybeSpawnFlags, var maybeExtraSpawnInfo_FromVisualSet) = GetMetadata(visualFromSetID);
        var prefabType = prefabID.ID;
        if (isDummyVisual)
        {
            if (PrefabsFuncs.IsTile(prefabType))
            {
                prefabType = Prefabs.VisualTile;
            }

            if (!PrefabsFuncs.IsPurelyVisual(prefabType))
            {
                Logger.LogError($"Unrecognized prefab type for dummy visual: {prefabType}");
                return null;
            }
        }

        var maybeSpawnInfo = PrefabSpawnInfo.ForVisualFromSet(visualFromSetID);

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
            world.Set(newEntity, currentRoom.ID);
            if (maybeLayer != null)
            {
                world.Set(newEntity, maybeLayer.LayerID);
                maybeLayer.CachedEntities.Add(newEntity);
            }
        }

        if (maybeLayer != null)
        {
            world.Set(newEntity, new Depth(maybeLayer.Depth));
        }

        return newEntity;
    }

    public abstract bool Editor_IsVisualFullyTransparent(PositionInVisualSet posInVisualSet, VisualSetVariantID variantID);
    public abstract bool Editor_CanAddOrRemoveVisuals();
    public bool Editor_CanResizeColumnCount => Editor_CanAddOrRemoveVisuals();
    public bool Editor_CanDeleteOrCreate => Editor_CanAddOrRemoveVisuals();
    public abstract bool Editor_TrySetColumnCount(ushort newWidth); // returns false if we can't resize
    
    public bool Editor_TrySetVisualColor(PositionInVisualSet posInVisualSet, VisualSetVariantID variantID, Color newColor)
    {
        if (!CanSetVisualColor(variantID))
        {
            return false;
        }
        var variantTileSet = VariantSets[variantID.ID];
        variantTileSet.Editor_SetTileColorOverride(posInVisualSet, newColor);

        // FIXME: Update existing entities!!!

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
    public string Editor_Name;
#endif

    // NOTE: If any field is non-null, it completely overrides the base field of the TileSet.
    public Dictionary<PositionInVisualSet, 
        (PrefabID?, FiledEntity.Flags?, PrefabExtraSpawnInfo?)>  
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
            var (_, _, maybeExtraSpawnInfo) = TileMetadataOverrides[tilePosInSet];
            if (maybeExtraSpawnInfo.HasValue)
            {
                if (maybeExtraSpawnInfo.Value.ColorBlendOverride.HasValue)
                {
                    return maybeExtraSpawnInfo.Value.ColorBlendOverride.Value;
                }
            }
        }
        return Color.White;
    }

#if DEBUG
    public void Editor_SetTileColorOverride(PositionInVisualSet tilePosInSet, Color newColor)
    {
        if (TileMetadataOverrides.ContainsKey(tilePosInSet))
        {
            var (maybePrefabID, maybeFlags, maybeExtraSpawnInfo) = TileMetadataOverrides[tilePosInSet];
            PrefabExtraSpawnInfo newExtraSpawnInfo;
            if (maybeExtraSpawnInfo.HasValue)
            {
                newExtraSpawnInfo = maybeExtraSpawnInfo.Value;
            }
            else
            {
                newExtraSpawnInfo = new();
            }
            newExtraSpawnInfo.ColorBlendOverride = newColor;
            TileMetadataOverrides[tilePosInSet] = (maybePrefabID, maybeFlags, newExtraSpawnInfo);
        }
    }
#endif

    public (PrefabID?, FiledEntity.Flags?, PrefabExtraSpawnInfo?)? 
        GetTileTileMetadataOverride(PositionInVisualSet tilePosInSet)
    {
        if (TileMetadataOverrides.ContainsKey(tilePosInSet))
        {
            return TileMetadataOverrides[tilePosInSet];
        }
        return null;
    }
}