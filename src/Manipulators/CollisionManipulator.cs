#define ShowDebugRaycastVisuals

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
using Filter = MoonTools.ECS.Filter;

public class CollisionManipulator : MoonTools.ECS.Manipulator
{
    //SpatialHash<Entity> InteractSpatialHash = new SpatialHash<Entity>(0, 0, Dimensions.GAME_W, Dimensions.GAME_H, 32);
    public static SpatialHash<Entity> CollidersSpatialHash =
        new SpatialHash<Entity>(0, 0, Dimensions.GAME_W, Dimensions.GAME_H, 32);

    public static HashSet<Entity> HitEntities = new HashSet<Entity>();
    public static Dictionary<Entity, Vector2> RaycastHits = new();

    public Filter CollisionFilter;

    public CollisionManipulator(World world) : base(world)
    {
        CollisionFilter = FilterBuilder
        .Include<Position>()
        .Include<Rectangle>()
        .Include<Layer>()
        .Build();

    }

    /*
    void ClearCanBeHeldSpatialHash()
    {
        InteractSpatialHash.Clear();
    }
    */
    public void ResetCollidersSpatialHash()
    {
        CollidersSpatialHash.Clear();

        foreach (var entity in CollisionFilter.Entities)
        {
            var position = Get<Position>(entity);
            var rect = Get<Rectangle>(entity);
            CollidersSpatialHash.Insert(entity, rect.GetWorldRect(position));
        }
    }

    bool CanMoveThroughDespiteCollision(
        Layer otherLayer,
        CollisionLayer canMoveLayer = 0
        )
    {
        return (canMoveLayer & otherLayer.Collide) != 0;
    }

    // Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
    bool CheckCollisionFlags(
        Entity other,
        CollisionLayer collideLayer,
        CollisionLayer excludeLayer = CollisionLayer.None
        )
    {
        var otherLayer = Get<Layer>(other);

        if ((collideLayer & otherLayer.Collide) != 0 &&
            (excludeLayer & otherLayer.Collide) == 0
            )
        {
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
                if (CheckCollisionFlags(other, collideLayer, excludeLayer))
                {
                    HitEntities.Add(other);
                    stopMovement = !CanMoveThroughDespiteCollision(Get<Layer>(other), canMoveLayer);
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

    Entity Debug_ShowRay(Position rayOrigin, float rayAngle, float length)
    {
        var entity = CreateEntity();
        Set(entity, new Timer(-1)); // lasts 1 frame
        Set(entity, rayOrigin);
        Set(entity, new SpriteAnimation(SpriteAnimations.Pixel));
        Set(entity, new ColorBlend(Color.Red with { A = 100 }));
        Set(entity, new Angle(rayAngle));

        // Stretch the pixel to form a line
        Set(entity, new SpriteScale(new Vector2(length, 1f)));

        return entity;
    }

    Entity Debug_ShowCollisionPos(Position pos)
    {
        var entity = CreateEntity();
        Set(entity, new Timer(-1)); // lasts 1 frame
        Set(entity, new SpriteAnimation(SpriteAnimations.Pixel));
        {
            // Center position according to scale.
            var scale = new Vector2(5f, 5f);
            Set(entity, new SpriteScale(scale));
            // Don't have to account for height/width of sprite, since both are 1.
            Set(entity, new Position(pos.X - MathF.Floor(scale.X * 0.5f), pos.Y - MathF.Floor(scale.Y * 0.5f)));
        }
        Set(entity, new ColorBlend(Color.LimeGreen));
        Set(entity, new Depth(-100f)); // draw above most things
        return entity;
    }

    void Debug_ShowEntityHasBeenCollided(Entity collided)
    {
        var timer = CreateEntity();
        Set(timer, new Timer(-1)); // lasts 1 frame
        Relate(collided, timer, new ColorBlendOverride(Color.Bisque));
    }

    void Debug_ShowHashCellCollision(Rectangle worldRect)
    {
        var entity = CreateEntity();
        Set(entity, new Timer(-1)); // lasts 1 frame
        Set(entity, new Position(worldRect.Left, worldRect.Top));
        Set(entity, new SpriteAnimation(SpriteAnimations.Pixel));
        Set(entity, new ColorBlend(Color.LightGreen with { A = 100 }));
        Set(entity, new SpriteScale(new Vector2(worldRect.Width, worldRect.Height)));
        Set(entity, new Depth(100f)); // draw behind most things
    }

    public (bool hit, Entity? stoppedAtEntity) Raycast_vs_AABBs(
        Entity source,
        float angle,
        float maxDistance,
        CollisionLayer rayLayer,
        CollisionLayer canMoveLayer = CollisionLayer.None
        )
    {
        RaycastHits.Clear();

        var direction = MathUtilities.SafeNormalize(new Vector2(MathF.Cos(angle), MathF.Sin(angle))) * maxDistance;
        var invDir = new Vector2(1, 1) / direction;

        Position startPos = Get<Position>(source);

        // TEST!!!
        //Position endPos = new Position(startPos.AsVector() + direction);
        //direction = endPos.AsVector() - startPos.AsVector(); // same result, we're good
        // TEST!!!

        var startVec = startPos.AsVector();
        var spatialHashCellAABB = new Rectangle(0, 0, CollidersSpatialHash.CellSize, CollidersSpatialHash.CellSize);

#if ShowDebugRaycastVisuals
        Debug_ShowRay(startPos, angle, maxDistance);
        Console.WriteLine($"Doing raycast. Start pos: {startPos}");
#endif

        // Check which grids we collide with from our spatial acceleration structure.
        // Also called a "broadphase".
        for (int row = 0; row < CollidersSpatialHash.RowCount; ++row)
        {
            for (int col = 0; col < CollidersSpatialHash.ColumnCount; ++col)
            {
                var cellPos = new Vector2(col * CollidersSpatialHash.CellSize, row * CollidersSpatialHash.CellSize);
                var cellRect = spatialHashCellAABB.GetWorldRect(cellPos);

                // Only check what's inside the grids we collide with.
                var (hitGrid, _) = RayCollision.Intersects_AABB(startVec, direction, invDir, cellRect/*cellRect.TopLeft(), cellRect.BottomRight()*/);
                if (hitGrid)
                {
#if ShowDebugRaycastVisuals
                    Debug_ShowHashCellCollision(cellRect);
#endif

                    // Do raycast checks with every AABB entity in this cell.
                    var others = CollidersSpatialHash.Cells[row][col];
                    foreach (var other in others)
                    {
                        if (source == other)
                        {
                            continue;
                        }
                        var otherRect = Get<Rectangle>(other).GetWorldRect(Get<Position>(other));
                        var (hit, hitPos) = RayCollision.Intersects_AABB(startVec, direction, invDir, otherRect/*otherRect.TopLeft(), otherRect.BottomRight()*/);
                        if (hit)
                        {
                            if (CheckCollisionFlags(other, rayLayer))
                            {
#if ShowDebugRaycastVisuals
                                Console.WriteLine($"Raycast hit at: {hitPos}");
                                Debug_ShowCollisionPos(new Position(hitPos));
                                Debug_ShowEntityHasBeenCollided(other);
#endif

                                if (RaycastHits.ContainsKey(other))
                                {
                                    // Only store closest collision to ray.
                                    if (Vector2.DistanceSquared(startVec, hitPos) < Vector2.DistanceSquared(startVec, RaycastHits[other]))
                                    {
                                        RaycastHits[other] = hitPos;
                                    }
                                }
                                else
                                {
                                    RaycastHits.Add(other, hitPos);
                                }

                                if ((canMoveLayer & Get<Layer>(other).Collide) == 0)
                                {
                                    // Can't move through this collision; ray stops.
                                    return (true, other);
                                }
                            }
                            else
                            {
#if ShowDebugRaycastVisuals
                                Console.WriteLine($"Raycast hit, but collision flags don't match.");
#endif
                            }
                        }
                    }
                }
            }
        }

        return (RaycastHits.Count != 0, null);
    }

    // Useful when the possible collision objects all occupy the same size on a grid, such as walls.
    // TODO: Use this algorithm: https://lodev.org/cgtutor/raycasting.html
    /*
    public void Raycast_TileGridOptimized(Entity source, )
    {

    }*/
}