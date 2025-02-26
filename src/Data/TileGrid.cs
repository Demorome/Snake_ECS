using System;
using System.Collections.Generic;

//using MoonTools.ECS.Collections;
using MoonTools.ECS;
using MoonWorks;
using Snake.Components;
using System.Numerics;

namespace Snake;

public class TileGrid
{
    public Entity[,] Grid;
    World World;

    public TileGrid(World world)
    {
        World = world;
        Grid = new Entity[GridInfo.WidthWithWalls, GridInfo.HeightWithWalls]; // [Row, Column]
    }

    public bool IsSpaceOccupiedBySolid(int x, int y)
    {
        return World.Has<Solid>(Grid[x, y]);
    }

    public void UpdateTilePosition(Entity e, Vector2 newPos)
    {
        var lastPos = World.Get<TilePosition>(e).Position;

        Grid[(int)newPos.X, (int)newPos.Y] = e;

        World.Set(e, new LastTilePosition(World.Get<TilePosition>(e).Position));
        World.Set(e, new TilePosition(newPos));
        World.Set(e, new PixelPosition(GridInfo.TilePositionToPixelPosition(newPos)));

        World.Set(e, new LastMovedDirection(newPos - lastPos));
    }
}