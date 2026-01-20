using System;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks.Math;
using RollAndCash;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Data;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class TileManipulator : MoonTools.ECS.Manipulator
{
    public TileManipulator(World world) : base(world)
    {
    }

    public Entity SpawnVisualTile(Position2D position, TileID tileID)
    {
        var entity = CreateEntity();
        Set(entity, position);
        Set(entity, tileID);
        Set(entity, new DestroyOnTransition());
        return entity;
    }

    public Entity SpawnRegularSolidTile(Position2D position, TileID tileID)
    {
        var entity = CreateEntity();
        Set(entity, position);
        Set(entity, new Rectangle(-Dimensions.TILE_SIZE / 2, -Dimensions.TILE_SIZE / 2,
            Dimensions.TILE_SIZE, Dimensions.TILE_SIZE));
        Set(entity, new Layer(CollisionLayer.Level, CollisionLayer.StaticLevelCollider_CollidesWith));
        Set(entity, new Depth(DepthLayer.SolidObject));
        Set(entity, tileID);
        Set(entity, new DestroyOnTransition());
        return entity;
    }

    public static Vector2? GetTilePos(Position2D worldPos)
    {
        var tilePos = new Vector2(worldPos.X / Dimensions.TILE_SIZE, worldPos.Y / Dimensions.TILE_SIZE);
        if (!IsTilePosValid(tilePos))
        {
            return null;
        }
        return tilePos;
    }

    public static bool IsTilePosValid(Vector2 tilePos)
    {
        if (tilePos.X < 0 || tilePos.X >= Dimensions.TILEGRID_COLUMNS)
        {
            return false;
        }
        else if (tilePos.Y < 0 || tilePos.Y >= Dimensions.TILEGRID_ROWS)
        {
            return false;
        }
        return true;
    }

    public Position2D TilePosToWorldPos_TopLeft(Vector2 tilePos)
    {
        return new Position2D(TilePosXToWorldPos_TopLeft((int)tilePos.X),
            TilePosYToWorldPos_TopLeft((int)tilePos.Y));
    }

    public Position2D TilePosToWorldPos_TopLeft(int x, int y)
    {
        return new Position2D(TilePosXToWorldPos_TopLeft(x),
            TilePosYToWorldPos_TopLeft(y));
    }

    float TilePosXToWorldPos_TopLeft(int tilePosX)
    {
        return tilePosX * Dimensions.TILE_SIZE;
    }

    float TilePosYToWorldPos_TopLeft(int tilePosY)
    {
        return tilePosY * Dimensions.TILE_SIZE;
    }
    
    public Position2D TilePosToWorldPos_Centered(Vector2 tilePos)
    {
        var offsetForCenterOfTile = Dimensions.TILE_SIZE / 2;
        return new Position2D(tilePos.X * Dimensions.TILE_SIZE + offsetForCenterOfTile,
            tilePos.Y * Dimensions.TILE_SIZE + offsetForCenterOfTile);
    }
}