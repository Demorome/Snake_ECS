using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using MoonTools.ECS;
using MoonWorks.Graphics;
using RollAndCash;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class CollisionManipulator : MoonTools.ECS.Manipulator
{
    //SpatialHash<Entity> InteractSpatialHash = new SpatialHash<Entity>(0, 0, Dimensions.GAME_W, Dimensions.GAME_H, 32);
    public static SpatialHash<Entity> CollidersSpatialHash = 
        new SpatialHash<Entity>(0, 0, Dimensions.GAME_W, Dimensions.GAME_H, 32);

    public static HashSet<Entity> HitEntities = new HashSet<Entity>();

    /*
    void ClearCanBeHeldSpatialHash()
    {
        InteractSpatialHash.Clear();
    }
    */
    public static void ClearCollidersSpatialHash()
    {
        CollidersSpatialHash.Clear();
    }

    public CollisionManipulator(World world) : base(world)
    {
    }

    public static Rectangle GetWorldRect(Position p, Rectangle r)
    {
        return new Rectangle(p.X + r.X, p.Y + r.Y, r.Width, r.Height);
    }

    public static Rectangle GetWorldRect(Vector2 p, Rectangle r)
    {
        return new Rectangle((int)(p.X + r.X), (int)(p.Y + r.Y), r.Width, r.Height);
    }

    public bool CanMoveThroughDespiteCollision(
        Layer otherLayer,
        CollisionLayer canMoveLayer = 0
        )
    {
        return (canMoveLayer & otherLayer.Collide) != 0;
    }

    // Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
    public bool CheckFlagsToRegisterCollision(
        Entity other,
        CollisionLayer collideLayer,
        CollisionLayer excludeLayer = 0
        )
    {
        var otherLayer = Get<Layer>(other);

        if ((collideLayer & otherLayer.Collide) != 0 &&
            (excludeLayer & otherLayer.Collide) == 0
            )
        {
            HitEntities.Add(other);
            return true;
        }
        return false;
    }


    // Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
    public bool CheckCollisions_AABB_vs_AABBs(
        Entity source, // so we can exclude it 
        Rectangle worldPosRect, 
        CollisionLayer collideLayer,
        CollisionLayer excludeLayer = 0,
        CollisionLayer canMoveLayer = 0
        )
    {
        bool stopMovement = false;

        foreach (var (other, otherRect) in CollidersSpatialHash.Retrieve(source, worldPosRect))
        {
            if (worldPosRect.Intersects(otherRect))
            {
                if (CheckFlagsToRegisterCollision(other, collideLayer, excludeLayer))
                {
                    stopMovement = CanMoveThroughDespiteCollision(Get<Layer>(other), canMoveLayer);
                }
            }
        }

        return stopMovement;
    }

    public bool CheckCollisions_AABB_vs_AABBs(Entity e, Rectangle worldPosRect)
    {
        var layer = Get<Layer>(e);
        var canMoveLayer = Has<CanMoveThroughDespiteCollision>(e) ? Get<CanMoveThroughDespiteCollision>(e).Value : 0;
        return CheckCollisions_AABB_vs_AABBs(e, worldPosRect, layer.Collide, layer.Exclude, canMoveLayer);
    }

    public (bool hit, Position stopPos) RaycastAABB(
        Entity source, 
        float angle, 
        float maxDistance, 
        CollisionLayer rayLayer,
        CollisionLayer canMoveLayer = 0 // TODO: Handle this in some special way
        )
    {
        HitEntities.Clear();

        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        Position startPos = Get<Position>(source);
        var startVec = startPos.AsVector();
        Rectangle spatialHashCellAABB = new Rectangle(0, 0, CollidersSpatialHash.CellSize, CollidersSpatialHash.CellSize);

        // TODO: Check which grids we collide with from our spatial acceleration structure.
        // Also called a "broadphase".
        for (int row = 0; row < CollidersSpatialHash.RowCount; ++row)
        {
            for (int col = 0; col < CollidersSpatialHash.ColumnCount; ++col)
            {
                var cellPos = new Vector2(col * CollidersSpatialHash.CellSize, row * CollidersSpatialHash.CellSize);
                var cellRect = GetWorldRect(cellPos, spatialHashCellAABB);

                // Only check what's inside the grids we collide with.
                var (t_min, t_max) = RayCollision.Intersect(startVec, direction, cellRect.TopLeft(), cellRect.BottomRight());
                if (t_min <= t_max) // if didn't miss
                {
                    // Do raycast checks with every AABB entity in this cell.
                    var others = CollidersSpatialHash.Cells[row][col];
                    foreach (var other in others)
                    {
                        (t_min, t_max) = RayCollision.Intersect(startVec, direction, cellRect.TopLeft(), cellRect.BottomRight());
                        if (t_min <= t_max) // if didn't miss
                        {
                            if (CheckFlagsToRegisterCollision(other, rayLayer))
                            {
                                // TODO: show t_min, t_max visuals
                            }
                        }
                    }
                }
            }
        }

        return (HitEntities.Count != 0, );
    }

    // Useful when the possible collision objects all occupy the same size on a grid, such as walls.
    // TODO: Use this algorithm: https://lodev.org/cgtutor/raycasting.html
    /*
    public void Raycast_TileGridOptimized(Entity source, )
    {

    }*/
}