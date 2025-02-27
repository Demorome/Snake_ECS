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

    public void EmptyOutTile(int x, int y)
    {
        Grid[x, y] = default;
    }

    public void DestroyAndReclaimTileSpace(Entity entity)
    {
        if (World.Has<TilePosition>(entity))
        {
            var position = World.Get<TilePosition>(entity);
            EmptyOutTile(position.X, position.Y);
        }
        World.Destroy(entity);
    }

    public bool IsTileEmpty(int x, int y)
    {
        return Grid[x, y] == default;
    }

    public bool IsSpaceOccupiedBySolid(int x, int y)
    {
        if (!IsTileEmpty(x, y))
        {
            return World.Has<Solid>(Grid[x, y]);
        }
        return false;
    }

    public bool IsSpaceOccupiedByPlayer(int x, int y)
    {
        if (!IsTileEmpty(x, y))
        {
            var entity = Grid[x, y];
            if (World.Has<TopParent>(entity))
            {
                entity = World.Get<TopParent>(entity).Parent;
            }
            return World.Has<PlayerIndex>(entity);
        }
        return false;
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

    public Vector2 GetSafeSpawnPosition()
    {
        while (true)
        {
            // Accounting for Walls taking up (0,0) etc.
            int row = Utility.Rando.Int(1, GridInfo.Height + 1);
            int col = Utility.Rando.Int(1, GridInfo.Width + 1);

            if (IsTileEmpty(row, col))
            {
                return new Vector2(row, col);
            }
        }
    }
}