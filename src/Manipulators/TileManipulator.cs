using System;
using System.ComponentModel;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks.Math;
using RollAndCash;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class TileManipulator : MoonTools.ECS.Manipulator
{
    public TileManipulator(World world) : base(world)
    {
    }

    public Entity SpawnSolidTile(Position2D position, SpriteAnimation sprite)
    {
        var entity = CreateEntity("Solid Tile");
        Set(entity, position);
        Set(entity, sprite);
        Set(entity, new Rectangle(-Dimensions.TILE_SIZE / 2, -Dimensions.TILE_SIZE / 2,
            Dimensions.TILE_SIZE, Dimensions.TILE_SIZE));

        Set(entity, new Layer(CollisionLayer.Level, CollisionLayer.StaticLevelCollider_CollidesWith));
        Set(entity, new Depth(7)); // draw below player (depth 5)

        return entity;
    }

    public Vector2? GetTilePos(Position2D worldPos)
    {
        var tilePos = new Vector2(worldPos.X / Dimensions.TILE_SIZE, worldPos.Y / Dimensions.TILE_SIZE);
        if (tilePos.X < 0 || tilePos.X >= Dimensions.TILE_COLUMN_COUNT)
        {
            return null;
        }
        else if (tilePos.Y < 0 || tilePos.Y >= Dimensions.TILE_ROW_COUNT)
        {
            return null;
        }
        return tilePos;
    }

    public Position2D TilePosToWorldPos(Vector2 tilePos)
    {
        return new Position2D(tilePos.X * Dimensions.TILE_SIZE,
            tilePos.Y * Dimensions.TILE_SIZE);
    }
    
    public Position2D TilePosToWorldPos_Centered(Vector2 tilePos)
    {
        var offsetForCenterOfTile = Dimensions.TILE_SIZE / 2;
        return new Position2D(tilePos.X * Dimensions.TILE_SIZE + offsetForCenterOfTile,
            tilePos.Y * Dimensions.TILE_SIZE + offsetForCenterOfTile);
    }
}