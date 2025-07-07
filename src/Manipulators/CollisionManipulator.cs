//#define ShowDebugRaycastVisuals

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

    public bool CanMoveThroughDespiteCollision(
        Layer otherLayer,
        CollisionLayer canMoveLayer = 0
        )
    {
        return (canMoveLayer & otherLayer.ExistsOn) != 0;
    }

    // Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
    bool CheckCollisionFlags(
        Entity other,
        CollisionLayer existsOnLayer,
        CollisionLayer collidesWithLayer
        )
    {
        var otherLayer = Get<Layer>(other);

        if ((collidesWithLayer & otherLayer.ExistsOn) != 0)
        {
            return true;
        }
#if DEBUG
        /*
        else if ((existsOnLayer & otherLayer.CollideWith) != 0)
        {
            Console.WriteLine("WARN: Entity B collides with A, but A doesn't with B.");
            return true;
        }*/
#endif
        return false;
    }


    // Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
    public bool CheckCollisions_AABB_vs_AABBs(
        Entity source, // so we can exclude it 
        Rectangle worldPosRect,
        CollisionLayer existsOnLayer,
        CollisionLayer collideWithLayer,
        CollisionLayer canMoveLayer = 0
        )
    {
        bool stopMovement = false;

        foreach (var (other, otherRect) in CollidersSpatialHash.Retrieve(source, worldPosRect))
        {
            if (worldPosRect.Intersects(otherRect))
            {
                if (CheckCollisionFlags(other, existsOnLayer, collideWithLayer))
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
        return CheckCollisions_AABB_vs_AABBs(e, worldPosRect, layer.ExistsOn, layer.CollideWith, canMoveLayer);
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
        Relate(collided, timer, new ColorBlendOverride(Color.Brown));
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
        Vector2 direction,
        float maxDistance,
        Layer rayLayer,
        CollisionLayer canMoveLayer = CollisionLayer.None
    )
    {
        RaycastHits.Clear();

        var rayVec = direction * maxDistance;
        var invRayVec = new Vector2(1, 1) / rayVec;
        Position startPos = Get<Position>(source);

        var startVec = startPos.AsVector();
        var spatialHashCellAABB = new Rectangle(0, 0, CollidersSpatialHash.CellSize, CollidersSpatialHash.CellSize);

#if ShowDebugRaycastVisuals
        Debug_ShowRay(startPos, MathUtilities.AngleFromUnitVector(direction), maxDistance);
        //Console.WriteLine($"Doing raycast. Start pos: {startPos}");
#endif

        Entity? maybeImpactedEntity = null; // to track if ray got stopped on something

        // Check which grids we collide with from our spatial acceleration structure.
        // Also called a "broadphase".
        for (int row = 0; row < CollidersSpatialHash.RowCount; ++row)
        {
            for (int col = 0; col < CollidersSpatialHash.ColumnCount; ++col)
            {
                var cellPos = new Vector2(col * CollidersSpatialHash.CellSize, row * CollidersSpatialHash.CellSize);
                var cellRect = spatialHashCellAABB.GetWorldRect(cellPos);

                // Only check what's inside the grids we collide with.
                var (hitGrid, _) = RayCollision.Intersects_AABB(startVec, rayVec, invRayVec, cellRect/*cellRect.TopLeft(), cellRect.BottomRight()*/);
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
                        var (hit, hitPos) = RayCollision.Intersects_AABB(startVec, rayVec, invRayVec, otherRect/*otherRect.TopLeft(), otherRect.BottomRight()*/);
                        if (!hit)
                        {
                            continue;
                        }

                        if (!CheckCollisionFlags(other, rayLayer.ExistsOn, rayLayer.CollideWith))
                        {
                            continue;
                        }
                        
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

                            // Can't move through this collision; ray stops.
                            // However, it's possible that there is a closer entity that we should stop on instead.
                            // Thus, we keep the loop going and track the impacted entity.
                            if (maybeImpactedEntity.HasValue)
                            {
                                // Check if this collision is closer than the previous ray-stopping entity impact.
                                // Otherwise, the ray will never reach it.
                                if (Vector2.DistanceSquared(startVec, hitPos) >= Vector2.DistanceSquared(startVec, RaycastHits[maybeImpactedEntity.Value]))
                                {
                                    continue;
                                }
                            }

                            // Is this a closer ray-stopping impact?
                            if (!CanMoveThroughDespiteCollision(Get<Layer>(other), canMoveLayer))
                            {
                                maybeImpactedEntity = other;
                            }

                            RaycastHits.Add(other, hitPos);
                        }
                    }
                }
            }
        }

        if (maybeImpactedEntity.HasValue)
        {
            var nearestCollision = RaycastHits[maybeImpactedEntity.Value];

            // Discard all impacts from RaycastHits that are behind the impact point.
            // FIXME: Ensure it's safe and portable to remove from a Dictionary mid-loop
            foreach (var (other, hitPos) in RaycastHits)
            {
                if (other == maybeImpactedEntity.Value)
                {
                    continue;
                }

                if (Vector2.DistanceSquared(startVec, hitPos) >= Vector2.DistanceSquared(startVec, nearestCollision))
                {
                    RaycastHits.Remove(other);
                }
            }
        }

#if ShowDebugRaycastVisuals
        foreach (var (other, hitPos) in RaycastHits)
        {
            //Console.WriteLine($"Raycast hit at: {hitPos}");
            Debug_ShowCollisionPos(new Position(hitPos));
            Debug_ShowEntityHasBeenCollided(other);
        }
#endif

        return (RaycastHits.Count != 0, maybeImpactedEntity);
    }

    public (bool hit, Entity? stoppedAtEntity) Raycast_vs_AABBs(
        Entity source,
        float angle,
        float maxDistance,
        Layer rayLayer,
        CollisionLayer canMoveLayer = CollisionLayer.None
        )
    {
        var direction = MathUtilities.SafeNormalize(new Vector2(MathF.Cos(angle), MathF.Sin(angle)));
        return Raycast_vs_AABBs(source, direction, maxDistance, rayLayer, canMoveLayer);
    }

    // Useful when the possible collision objects all occupy the same size on a grid, such as walls.
    // TODO: Use this algorithm: https://lodev.org/cgtutor/raycasting.html
    /*
    public void Raycast_TileGridOptimized(Entity source, )
    {

    }*/
}