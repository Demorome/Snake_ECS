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
    public static Dictionary<Entity, Position2D> RaycastHits = new();

    public Filter CollisionFilter;

    public CollisionManipulator(World world) : base(world)
    {
        CollisionFilter = FilterBuilder
        .Include<Position2D>()
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
            var position = Get<Position2D>(entity);
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
    public bool CheckCollisions(
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
            if (!worldPosRect.Intersects(otherRect))
            {
                continue;
            }

            if (!CheckCollisionFlags(other, existsOnLayer, collideWithLayer))
            {
                continue;
            }

            if (Has<HasLineHitbox>(source))
            {
                if (Has<HasLineHitbox>(other))
                {
                    if (LineCollision.Line_vs_Line(GetLine(source), GetLine(other)) == null)
                    {
                        continue;
                    }
                }
                else
                {
                    // FIXME: If `source` is a line, use line vs AABB to check if it actually hit.
                }
            }
            else if (Has<HasLineHitbox>(other))
            {
                // FIXME: If `other` is a line, use AABB vs line to check if it actually hit.
            }

            HitEntities.Add(other);
            stopMovement = !CanMoveThroughDespiteCollision(Get<Layer>(other), canMoveLayer);
        }

        return stopMovement;
    }

    public bool CheckCollisions(Entity e, Rectangle worldPosRect)
    {
        var layer = Get<Layer>(e);
        var canMoveLayer = Has<CanMoveThroughDespiteCollision>(e) ? Get<CanMoveThroughDespiteCollision>(e).Value : 0;
        return CheckCollisions(e, worldPosRect, layer.ExistsOn, layer.CollideWith, canMoveLayer);
    }

    Entity Debug_ShowRay(Position2D rayOrigin, float rayAngle, float length)
    {
        var entity = CreateEntity("Raycast Visual");
        Set(entity, new Timer(-1)); // lasts 1 frame
        Set(entity, rayOrigin);
        Set(entity, new SpriteAnimation(SpriteAnimations.Pixel));
        Set(entity, new ColorBlend(Color.Red with { A = 100 }));
        Set(entity, new Angle(rayAngle));

        // Stretch the pixel to form a line
        Set(entity, new VisualScale(new Vector2(length, 1f)));

        return entity;
    }

    Entity Debug_ShowCollisionPos(Position2D pos)
    {
        var entity = CreateEntity("Raycast Collision Pos Visual");
        Set(entity, new Timer(-1)); // lasts 1 frame
        Set(entity, new SpriteAnimation(SpriteAnimations.Pixel));
        {
            // Center position according to scale.
            var scale = new Vector2(5f, 5f);
            Set(entity, new VisualScale(scale));
            // Don't have to account for height/width of sprite, since both are 1.
            Set(entity, new Position2D(pos.X - MathF.Floor(scale.X * 0.5f), pos.Y - MathF.Floor(scale.Y * 0.5f)));
        }
        Set(entity, new ColorBlend(Color.LimeGreen));
        Set(entity, new Depth(DepthLayer.Debug_CollisionVisual)); // draw above most things
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
        Set(entity, new Position2D(worldRect.Left, worldRect.Top));
        Set(entity, new SpriteAnimation(SpriteAnimations.Pixel));
        Set(entity, new ColorBlend(Color.LightGreen with { A = 100 }));
        Set(entity, new VisualScale(new Vector2(worldRect.Width, worldRect.Height)));
        Set(entity, new Depth(DepthLayer.Debug_CollisionVisual)); // draw behind most things
    }

    public Position2D? Raycast_vs_Collider(
        Entity source,
        Position2D startPos,
        Layer rayLayer,
        Vector2 rayVec,
        Vector2 invRayVec,
        Entity other
    )
    {
        if (!CheckCollisionFlags(other, rayLayer.ExistsOn, rayLayer.CollideWith))
        {
            return null;
        }

        if (Has<HasLineHitbox>(other))
        {
            return LineCollision.Line_vs_Line(
                new LineCollision.Line(
                    startPos,
                    startPos + new Position2D(rayVec)
                ),
                GetLine(other)
            );
        }
        else
        {
            var otherRect = Get<Rectangle>(other).GetWorldRect(Get<Position2D>(other));
            return RayCollision.Intersects_AABB(
                startPos, rayVec, invRayVec, otherRect/*otherRect.TopLeft(), otherRect.BottomRight()*/
            );
        }
    }

    public (bool hit, Entity? maybeStoppedAtEntity) Raycast_vs_Colliders(
        Entity source,
        Vector2 direction,
        float maxDistance,
        Layer rayLayer,
        CollisionLayer canMoveLayer = CollisionLayer.None
    )
    {
        RaycastHits.Clear();

        var rayVec = direction * maxDistance;
        var invRayVec = Vector2.One / rayVec;
        Position2D startPos = Get<Position2D>(source);

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
                var hitGrid = RayCollision.Intersects_AABB(
                    startPos, rayVec, invRayVec, cellRect/*cellRect.TopLeft(), cellRect.BottomRight()*/
                ).HasValue;
                
                if (hitGrid)
                {
#if ShowDebugRaycastVisuals
                    //Debug_ShowHashCellCollision(cellRect);
#endif

                    // Do raycast checks with every AABB entity in this cell.
                    var others = CollidersSpatialHash.Cells[row][col];
                    foreach (var other in others)
                    {
                        if (source == other)
                        {
                            continue;
                        }

                        var maybeHitPos = Raycast_vs_Collider(source, startPos,
                            rayLayer, rayVec, invRayVec, other
                        );

                        if (!maybeHitPos.HasValue)
                        {
                            continue;
                        }
                        var hitPos = maybeHitPos.Value;
                        var hitVec = hitPos.AsVector();
                        
                        if (RaycastHits.ContainsKey(other))
                        {
                            // Only store closest collision to ray.
                            if (Vector2.DistanceSquared(startVec, hitVec) 
                                < Vector2.DistanceSquared(startVec, RaycastHits[other].AsVector()))
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
                                if (Vector2.DistanceSquared(startVec, hitVec) 
                                    >= Vector2.DistanceSquared(startVec, RaycastHits[maybeImpactedEntity.Value].AsVector()))
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
            var nearestCollision = RaycastHits[maybeImpactedEntity.Value].AsVector();

            // Discard all impacts from RaycastHits that are behind the impact point.
            // FIXME: Ensure it's safe and portable to remove from a Dictionary mid-loop
            foreach (var (other, hitPos) in RaycastHits)
            {
                if (other == maybeImpactedEntity.Value)
                {
                    continue;
                }

                if (Vector2.DistanceSquared(startVec, hitPos.AsVector()) 
                    >= Vector2.DistanceSquared(startVec, nearestCollision))
                {
                    RaycastHits.Remove(other);
                }
            }
        }

#if ShowDebugRaycastVisuals
        foreach (var (other, hitPos) in RaycastHits)
        {
            //Console.WriteLine($"Raycast hit at: {hitPos}");
            Debug_ShowCollisionPos(hitPos);
            Debug_ShowEntityHasBeenCollided(other);
        }
#endif

        return (RaycastHits.Count != 0, maybeImpactedEntity);
    }

    public (bool hit, Entity? stoppedAtEntity) Raycast_vs_Colliders(
        Entity source,
        float angle,
        float maxDistance,
        Layer rayLayer,
        CollisionLayer canMoveLayer = CollisionLayer.None
        )
    {
        var direction = MathUtilities.SafeNormalize(new Vector2(MathF.Cos(angle), MathF.Sin(angle)));
        return Raycast_vs_Colliders(source, direction, maxDistance, rayLayer, canMoveLayer);
    }

    // Useful when the possible collision objects all occupy the same size on a grid, such as walls.
    // TODO: Use this algorithm: https://lodev.org/cgtutor/raycasting.html
    /*
    public void Raycast_TileGridOptimized(Entity source, )
    {

    }*/

    public LineCollision.Line GetLine(Entity e)
    {
        return LineCollision.GetLine(e, World);
    }
}