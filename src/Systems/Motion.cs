using System;
using System.Numerics;
using MoonTools.ECS;
using RollAndCash.Utility;
using RollAndCash.Components;
using RollAndCash.Relations;
using RollAndCash.Messages;
using RollAndCash.Content;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace RollAndCash.Systems;

public class Motion : MoonTools.ECS.System
{
    float HighSpeedMin = 1000f;

    CollisionManipulator CollisionManipulator;

    Filter SpeedFilter;
    //Filter InteractFilter;
    Filter AccelerateToPositionFilter;
   
    public Motion(World world) : base(world)
    {
        CollisionManipulator = new CollisionManipulator(world);

        SpeedFilter = 
        FilterBuilder
        .Include<Position>()
        .Include<Speed>()
        .Build();

        //InteractFilter = FilterBuilder.Include<Position>().Include<Rectangle>().Include<CanInteract>().Build();

        AccelerateToPositionFilter = 
        FilterBuilder
        .Include<Position>()
        .Include<AccelerateToPosition>()
        .Include<Speed>()
        .Build();
    }

    void HandleRegularCollisions(Entity e)
    {
        // Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
        foreach (var other in CollisionManipulator.HitEntities)
        {
            bool duplicate = false;
            foreach (var msg in ReadMessages<Collide>())
            {
                if (msg.A == other && msg.B == e)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
            {
                Send(new Collide(e, other));
            }
        }
    }

    void HandleHitscanCollisions(Entity e)
    {
        // Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
        foreach (var (other, _) in CollisionManipulator.RaycastHits)
        {
            bool duplicate = false;
            foreach (var msg in ReadMessages<Collide>())
            {
                if (msg.A == other && msg.B == e)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
            {
                Send(new Collide(e, other));
            }
        }
    }

    Position HighSpeedSweepTest(Entity e, float travelDistance, float dt)
    {
        var position = Get<Position>(e);
        var direction = Get<Direction>(e);
        var r = Get<Rectangle>(e);

        var movement = direction.Value * travelDistance * dt;
        var targetPosition = position + movement;

        var xEnum = new IntegerEnumerator(position.X, targetPosition.X);
        var yEnum = new IntegerEnumerator(position.Y, targetPosition.Y);
        var xEnumSize = Math.Abs(targetPosition.X - position.X);
        var yEnumSize = Math.Abs(targetPosition.Y - position.Y);
        var biggestSize = Math.Max(xEnumSize, yEnumSize);

        int mostRecentValidXPosition = position.X;
        int mostRecentValidYPosition = position.Y;

        CollisionManipulator.HitEntities.Clear();

        for (int i = 0; i < biggestSize; ++i)
        {
            var x = xEnum.Current;
            var y = yEnum.Current;
            var newPos = new Position(x, y);
            var rect = r.GetWorldRect(newPos);

            var stopMoving = CollisionManipulator.CheckCollisions_AABB_vs_AABBs(e, rect);
            if (stopMoving)
            {
                movement.X = mostRecentValidXPosition - position.X;
                position = position.SetX(position.X); // truncates x coord

                movement.Y = mostRecentValidYPosition - position.Y;
                position = position.SetY(position.Y); // truncates y coord
                break;
            }

            mostRecentValidXPosition = x;
            mostRecentValidYPosition = y;

            if (i < xEnumSize)
            {
                xEnum.MoveNext();
            }
            if (i < yEnumSize)
            {
                yEnum.MoveNext();
            }
        }
        return position + movement;
    }

    Position SweepTest(Entity e, Vector2 velocity, float dt)
    {
        var position = Get<Position>(e);
        var r = Get<Rectangle>(e);

        var movement = new Vector2(velocity.X, velocity.Y) * dt;
        var targetPosition = position + movement;

        var xEnum = new IntegerEnumerator(position.X, targetPosition.X);
        var yEnum = new IntegerEnumerator(position.Y, targetPosition.Y);

        int mostRecentValidXPosition = position.X;
        int mostRecentValidYPosition = position.Y;

        CollisionManipulator.HitEntities.Clear();
        bool xHit = false;
        bool yHit = false;

        foreach (var x in xEnum)
        {
            var newPos = new Position(x, position.Y);
            var rect = r.GetWorldRect(newPos);

            var stopMoving = CollisionManipulator.CheckCollisions_AABB_vs_AABBs(e, rect);

            xHit = stopMoving;

            if (xHit)
            {
                movement.X = mostRecentValidXPosition - position.X;
                position = position.SetX(position.X); // truncates x coord
                break;
            }

            mostRecentValidXPosition = x;
        }

        foreach (var y in yEnum)
        {
            var newPos = new Position(mostRecentValidXPosition, y);
            var rect = r.GetWorldRect(newPos);

            var stopMoving = CollisionManipulator.CheckCollisions_AABB_vs_AABBs(e, rect);
            yHit = stopMoving;

            if (yHit)
            {
                movement.Y = mostRecentValidYPosition - position.Y;
                position = position.SetY(position.Y); // truncates y coord
                break;
            }

            mostRecentValidYPosition = y;
        }

        return position + movement;
    }

    Position DoRegularMovement(Entity entity, Position entityPos, float baseSpeed, float secondsDelta)
    {
        var speed = baseSpeed;
        foreach (var otherEntity in OutRelations<SpeedMult>(entity))
        {
            var speedMult = GetRelationData<SpeedMult>(entity, otherEntity).Value;
            speed *= speedMult;
        }
        var vel = speed * Get<Direction>(entity).Value;

        if (Has<Rectangle>(entity) && Has<Layer>(entity)) // if has colliders
        {
            if (speed >= HighSpeedMin)
            {
                return HighSpeedSweepTest(entity, speed, secondsDelta);
            }
            else
            {
                return SweepTest(entity, vel, secondsDelta);
            }
        }
        else
        {
            var scaledVelocity = vel * secondsDelta;
            if (Has<ForceIntegerMovement>(entity))
            {
                scaledVelocity = new Vector2((int)scaledVelocity.X, (int)scaledVelocity.Y);
            }
            return entityPos + scaledVelocity;
        }
    }

    // FIXME: Currently ignores the entity's AABB; just shoots a thin ray.
    Position DoHitscanMovement(Entity e, float secondsDelta)
    {
        float hitscanSpeed = Get<HitscanSpeed>(e).Value;
        var direction = Get<Direction>(e).Value;
        //float maxDistance = Has<MaxMovementDistance>(e) ? Get<MaxMovementDistance>(e).Value : hitscanSpeed;
        float scaledVelocity = hitscanSpeed * secondsDelta;

        var rayLayer = Get<Layer>(e);
        var canMoveThroughLayer = Has<CanMoveThroughDespiteCollision>(e) ? Get<CanMoveThroughDespiteCollision>(e).Value : CollisionLayer.None;

        var (hit, stoppedAtEntity) = CollisionManipulator.Raycast_vs_AABBs(e, direction, scaledVelocity, rayLayer, canMoveThroughLayer);

        Vector2 endPos;
        if (stoppedAtEntity.HasValue)
        {
            endPos = CollisionManipulator.RaycastHits[stoppedAtEntity.Value];
        }
        else {
            endPos = Get<Position>(e).AsVector() + (direction * scaledVelocity);
        }

        // FIXME: Check if we've travelled the MaxDistance.
        // If so, stop any future movement.

        return new Position(endPos);
    }

    public override void Update(TimeSpan delta)
    {
        //ClearCanBeHeldSpatialHash();
        // TODO: make sure this isn't needed, i.e. it's called earlier in another system and no entities are deleted since then.
        //CollisionManipulator.ResetCollidersSpatialHash();

        /*
        foreach (var entity in InteractFilter.Entities)
        {
            var position = Get<Position>(entity);
            var rect = Get<Rectangle>(entity);

            InteractSpatialHash.Insert(entity, GetWorldRect(position, rect));
        }

        foreach (var entity in InteractFilter.Entities)
        {
            foreach (var other in OutRelations<Colliding>(entity))
            {
                Unrelate<Colliding>(entity, other);
            }
        }

        foreach (var entity in InteractFilter.Entities)
        {
            var position = Get<Position>(entity);
            var rect = GetWorldRect(position, Get<Rectangle>(entity));

            foreach (var (other, otherRect) in InteractSpatialHash.Retrieve(rect))
            {
                if (rect.Intersects(otherRect))
                {
                    Relate(entity, other, new Colliding());
                }

            }
        }*/

        // FIXME: Make moving level setpieces push moveable entities back, rather than preventing the setpiece from moving.
        foreach (var entity in SpeedFilter.Entities)
        {
            if (HasOutRelation<DontMove>(entity))
                continue;

            var pos = Get<Position>(entity);
            Set(entity, new LastPosition(pos.AsVector()));

            if (Has<HitscanSpeed>(entity))
            {
                pos = DoHitscanMovement(entity, (float)delta.TotalSeconds);
                Set(entity, pos);
                HandleHitscanCollisions(entity);
            }
            else
            {
                float baseSpeed = Get<Speed>(entity).Value;
                if (Has<SpeedAcceleration>(entity))
                {
                    baseSpeed *= Get<SpeedAcceleration>(entity).Value;
                }
                var baseVel = baseSpeed * Get<Direction>(entity).Value;

                // FIXME: Ensure it won't crash when going outside the screen position.
                pos = DoRegularMovement(entity, pos, baseSpeed, (float)delta.TotalSeconds);
                Set(entity, pos);
                HandleRegularCollisions(entity);

                if (Has<FallSpeed>(entity))
                {
                    var fallspeed = Get<FallSpeed>(entity).Speed;
                    baseVel += Vector2.UnitY * fallspeed;
                }

                if (Has<MotionDamp>(entity))
                {
                    var dampSpeed = Vector2.Distance(Vector2.Zero, baseVel) - Get<MotionDamp>(entity).Damping;
                    dampSpeed = MathF.Max(dampSpeed, 0);
                    baseVel = dampSpeed * MathUtilities.SafeNormalize(baseVel);
                }

                Set(entity, new Speed(baseVel.Length()));
                Set(entity, new Direction(MathUtilities.SafeNormalize(baseVel)));
            }

            if (Has<DestroyAtScreenBottom>(entity) && pos.Y > Dimensions.GAME_H - 32)
            {
                /*
                if (HasOutRelation<UpdateDisplayScoreOnDestroy>(entity))
                {
                    var outEntity = OutRelationSingleton<UpdateDisplayScoreOnDestroy>(entity);
                    var scoreEntity = OutRelationSingleton<HasScore>(outEntity);
                    var data = GetRelationData<UpdateDisplayScoreOnDestroy>(entity, outEntity);
                    var score = Get<DisplayScore>(scoreEntity).Value + (data.Negative ? -1 : 1);
                    Set(scoreEntity, new Text(Content.Fonts.KosugiID, FontSizes.SCORE, score.ToString()));
                    Set(scoreEntity, new DisplayScore(score));

                    // TODO: shouldn't tightly couple this exact money sound behavior to DestroyAtScreenBottom but hey it's a jam game
                    var pan = (((float)pos.X / Dimensions.GAME_W * 2f) - 1f) / 1.5f;
                    var pitch = .9f + (.1f * (float)score / 800);

                    Send(new PlayStaticSoundMessage(
                        Rando.GetRandomItem(AudioArrays.Coins),
                        Data.SoundCategory.Generic,
                        2f,
                        pitch,
                        pan
                    ));
                }*/

                Set(entity, new MarkedForDestroy());
                continue;
            }

            if (Has<DestroyWhenOutOfBounds>(entity))
            {
                if (pos.X < -100 || pos.X > Dimensions.GAME_W + 100 || pos.Y < -100 || pos.Y > Dimensions.GAME_H + 100)
                {
                    /*
                    foreach (var heldEntity in OutRelations<Holding>(entity))
                    {
                        Destroy(heldEntity);
                    }*/

                    Set(entity, new MarkedForDestroy());
                    continue;
                }
            }

            // update spatial hashes

            /*
            if (Has<CanInteract>(entity))
            {
                var position = Get<Position>(entity);
                var rect = Get<Rectangle>(entity);

                InteractSpatialHash.Insert(entity, GetWorldRect(position, rect));
            }*/

            if (Has<Layer>(entity) && Has<Rectangle>(entity))
            {
                var position = Get<Position>(entity);
                var rect = Get<Rectangle>(entity);
                CollisionManipulator.CollidersSpatialHash.Insert(entity, rect.GetWorldRect(position));
            }
        }

        foreach (var entity in CollisionManipulator.CollisionFilter.Entities)
        {
            UnrelateAll<TouchingSolid>(entity);
        }

        /*
        foreach (var entity in CollisionFilter.Entities)
        {
            var position = Get<Position>(entity);
            var rectangle = Get<Rectangle>(entity);

            var leftPos = new Position(position.X - 1, position.Y);
            var rightPos = new Position(position.X + 1, position.Y);
            var upPos = new Position(position.X, position.Y - 1);
            var downPos = new Position(position.X, position.Y + 1);

            var leftRectangle = GetWorldRect(leftPos, rectangle);
            var rightRectangle = GetWorldRect(rightPos, rectangle);
            var upRectangle = GetWorldRect(upPos, rectangle);
            var downRectangle = GetWorldRect(downPos, rectangle);

            var (leftOther, leftCollided) = CheckCollisions(entity, leftRectangle);
            var (rightOther, rightCollided) = CheckCollisions(entity, rightRectangle);
            var (upOther, upCollided) = CheckCollisions(entity, upRectangle);
            var (downOther, downCollided) = CheckCollisions(entity, downRectangle);

            if (leftCollided)
            {
                Relate(entity, leftOther, new TouchingSolid());
            }

            if (rightCollided)
            {
                Relate(entity, rightOther, new TouchingSolid());
            }

            if (upCollided)
            {
                Relate(entity, upOther, new TouchingSolid());
            }
            if (downCollided)
            {
                Relate(entity, downOther, new TouchingSolid());
            }
        }*/

        /*
        foreach (var entity in AccelerateToPositionFilter.Entities)
        {
            var velocity = Get<Velocity>(entity).Value;
            var position = Get<Position>(entity);
            var accelTo = Get<AccelerateToPosition>(entity);
            var difference = accelTo.Target - position;
            velocity /= accelTo.MotionDampFactor * (1 + (float)delta.TotalSeconds); // TODO: IDK if this is deltatime friction but game is fixed fps rn anyway
            velocity += MathUtilities.SafeNormalize(difference) * accelTo.Acceleration * (float)delta.TotalSeconds;
            Set(entity, new Velocity(velocity));
        }*/
    }
}
