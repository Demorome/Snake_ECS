using System;
using System.ComponentModel;
using System.Numerics;
using MoonTools.ECS;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

// Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
public class Collision : MoonTools.ECS.System
{
    CollisionManipulator CollisionManipulator;

    public Collision(World world) : base(world)
    {
        CollisionManipulator = new(World);
    }

    void HandleCollision(Entity movingEntity, Entity collidedEntity)
    {
        if (Has<HasHealth>(movingEntity) && Has<DealsDamageOnContact>(collidedEntity))
        {
            Send(new DealDamage(movingEntity, Get<DealsDamageOnContact>(collidedEntity).Damage));
        }
        if (Has<HasHealth>(collidedEntity) && Has<DealsDamageOnContact>(movingEntity))
        {
            Send(new DealDamage(collidedEntity, Get<DealsDamageOnContact>(movingEntity).Damage));
        }

        bool impacted = true; // if it stopped when colliding
        if (Has<CanMoveThroughDespiteCollision>(movingEntity))
        {
            var canMoveThroughLayer = Get<CanMoveThroughDespiteCollision>(movingEntity).Value;
            var otherLayer = Get<Layer>(collidedEntity).ExistsOn;
            if ((canMoveThroughLayer & otherLayer) != 0)
            {
                impacted = false;
            }
        }

        bool hasReflectedMovingEntity =
            impacted
            && Has<ReflectsProjectiles>(collidedEntity)
            && (Get<Layer>(movingEntity).ExistsOn & CollisionLayer.Projectile) != 0
        ;

        if (hasReflectedMovingEntity)
        {
            // If moving entity is a ray, assume the point of impact is its new location, since it just moved.
            // If the collided entity is a line, then we can get its normal for a more accurate reflection.
            if (Has<HitscanSpeed>(movingEntity) && Has<HasLineHitbox>(collidedEntity))
            {
                // Credits to samgak: https://stackoverflow.com/a/30971055
                // Also to MBo: https://stackoverflow.com/a/49096511
                // Calculate the line normal.
                // NOTE: It's the same no matter what side of the line has been hit.
                var collidedLine = LineCollision.GetLine(collidedEntity, World);
                float normalY = collidedLine.PointB.X - collidedLine.PointA.X;
                float normalX = collidedLine.PointA.Y - collidedLine.PointB.Y;
                var normalLength = float.Sqrt(normalX * normalX + normalY * normalY);
                var normalVec = new Vector2(normalX, normalY) / normalLength;

                var oldDirection = Get<Direction2D>(movingEntity).Value;
                //var dot = Vector2.Dot(oldDirection, normalVec);// oldDirection.X * normalVec.X + oldDirection.Y * normalVec.Y
                // TODO: Not sure if SafeNormalize is needed here? Just being safe
                //var newDirection = MathUtilities.SafeNormalize(oldDirection - 2 * dot * normalVec);
                var newDirection = MathUtilities.SafeNormalize(Vector2.Reflect(oldDirection, normalVec));
                //var newDirection = oldDirection - normalVec; // for Refraction?
                Set(movingEntity, new Direction2D(newDirection));

                // Get it unstuck from the collision point.
                // With the naive approach of pre-emptively pushing it in the new direction,
                // ... some jank stuff still gets through at select angles.
                var currentPos = Get<Position2D>(movingEntity);
                var previousPos = Get<LastPosition>(movingEntity).Value;
                //var posOffset = new Vector2(MathF.Ceiling(MathF.Abs(newDirection.X)), MathF.Ceiling(MathF.Abs(newDirection.Y)));
                //posOffset = Vector2.CopySign(posOffset, newDirection);
                //var posOffset = Vector2.Round(newDirection + Vector2.CopySign(new Vector2(0.5f, 0.5f), newDirection));
                //var posOffset = newDirection; //+ Vector2.CopySign(new Vector2(0.5f, 0.5f), newDirection);
                //var posOffset = newDirection + Vector2.CopySign(new Vector2(0.49f, 0.49f), newDirection);
                //Set(movingEntity, new Position2D(currentPos.AsVector() + posOffset));

                var rayLayer = Get<Layer>(movingEntity);
                //var rayVec = newDirection * Get<HitscanSpeed>(movingEntity).Value;
                var prevDistanceTravelled = currentPos.PixelDistance(previousPos);

                // Reduce prevDistanceTravelled until we're a pixel before the ray hit the collider.
                Position2D? maybeHitPos;
                do
                {
                    prevDistanceTravelled -= 1;
                    var rayVec = oldDirection * prevDistanceTravelled;
                    var invRayVec = Vector2.One / rayVec;

                    maybeHitPos = CollisionManipulator.Raycast_vs_Collider(
                        movingEntity, previousPos, //currentPos,
                        rayLayer, rayVec, invRayVec,
                        collidedEntity
                    );

                    if (prevDistanceTravelled < -1)
                    {
                        throw new Exception("What the hay!!");
                    }
                }
                while (maybeHitPos != null);

                Set(movingEntity, previousPos + new Position2D(oldDirection * prevDistanceTravelled));
            }
            else
            {
                // Otherwise, just flip the moving entity's direction to go directly back to its origin.
                var direction = Get<Direction2D>(movingEntity).Value;
                Set(movingEntity, new Direction2D(-direction));

                // FIXME: Get it unstuck from inside the collision point, if needed
            }
        }
        else if (impacted)
        {
            if (Has<DestroyOnImpact>(movingEntity))
            {
                Set(movingEntity, new MarkedForDestroy());
            }
        }
        
        if (Has<DestroyOnImpact>(collidedEntity))
        {
            Set(collidedEntity, new MarkedForDestroy());
        }
    }

    public override void Update(System.TimeSpan delta)
    {
        foreach (var message in ReadMessages<Collide>())
        {
            HandleCollision(message.A, message.B);
        }
    }

}