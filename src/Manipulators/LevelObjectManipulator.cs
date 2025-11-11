using System;
using System.ComponentModel;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks.Graphics;
using MoonWorks.Math;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class LevelObjectManipulator : MoonTools.ECS.Manipulator
{
    public LevelObjectManipulator(World world) : base(world)
    {
    }

    public Entity SpawnInvisibleSolidRectangle(Position2D position, int width = 32, int height = 16)
    {
        var entity = CreateEntity("SolidRectangle");
        Set(entity, position);
        Set(entity, new Rectangle(0, 0, width, height));
        Set(entity, new Layer(CollisionLayer.Level, CollisionLayer.StaticLevelCollider_CollidesWith));
        Set(entity, new DestroyOnTransition());
        return entity;
    }
    
    public Entity SpawnSolidRectangle(Position2D position, Color color, int width = 32, int height = 16)
    {
        var entity = SpawnInvisibleSolidRectangle(position, width, height);
        Set(entity, new DrawAsRectangle());
        Set(entity, new ColorBlend(color));
        Set(entity, new Depth(DepthLayer.SolidObject));
        Set(entity, new DestroyOnTransition());
        return entity;
    }
}