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
    public Collision(World world) : base(world)
    {
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
            // If moving entity is a line, assume the point of impact is its new location, since it just moved.
            // If the collided entity is also a line, then we can get its normal for a more accurate reflection.
            if ((Has<HasLineHitbox>(movingEntity) || Has<HitscanSpeed>(movingEntity))
                && Has<HasLineHitbox>(collidedEntity))
            {
                // Credits to samgak: https://stackoverflow.com/a/30971055
                // Also MBo: https://stackoverflow.com/a/49096511
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
                var newDirection = Vector2.Reflect(oldDirection, normalVec);
                //var newDirection = oldDirection - normalVec; // for Refraction?
                Set(movingEntity, new Direction2D(newDirection));

                // Get it unstuck from the collision point.This usually happens at 160 degree angles.
                // The problem is likely due to rounding for Position2D.
                // We can't just move in the new direction by 1 pixel in both directions, because we might outpace a sloped collided line in one direction.
                var currentPos = Get<Position2D>(movingEntity);
                //var posOffset = new Vector2(MathF.Ceiling(MathF.Abs(newDirection.X)), MathF.Ceiling(MathF.Abs(newDirection.Y)));
                //posOffset = Vector2.CopySign(posOffset, newDirection);
                //var posOffset = Vector2.Round(newDirection + Vector2.CopySign(new Vector2(0.5f, 0.5f), newDirection));
                //var posOffset = newDirection; //+ Vector2.CopySign(new Vector2(0.5f, 0.5f), newDirection);
                //var posOffset = newDirection + Vector2.CopySign(new Vector2(0.49f, 0.49f), newDirection);
                //Set(movingEntity, new Position2D(currentPos.AsVector() + posOffset));
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