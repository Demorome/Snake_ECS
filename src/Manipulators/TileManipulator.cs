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
}