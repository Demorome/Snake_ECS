using System;
using MoonTools.ECS;
using RollAndCash.Relations;
using RollAndCash.Components;
using RollAndCash.Utility;
using System.Numerics;

namespace RollAndCash.Systems;

public class FollowingSystem : MoonTools.ECS.System
{
    public FollowingSystem(World world) : base(world)
    {
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var (follower, target) in Relations<PositionFollowing>())
        {
            if (HasOutRelation<DontFollowTarget>(follower))
            {
                continue;
            }

            Set(follower, Get<Position>(target));
        }

        foreach (var (follower, target) in Relations<VisuallyFollowing>())
        {
            if (HasOutRelation<DontFollowTarget>(follower))
            {
                continue;
            }

            var followData = GetRelationData<VisuallyFollowing>(follower, target);

            var followerPos = Get<Position>(follower);
            var targetPos = Get<Position>(target);
            var distance = Vector2.Distance(targetPos.AsVector(), followerPos.AsVector());

            if (followData.LookTowards)
            {
                var orientation = MathF.Atan2(targetPos.Y - followerPos.Y, targetPos.X - followerPos.X);
                Set(follower, new Angle(orientation));
            }

            if (followData.StretchTowards)
            {
                // TODO: Account for sprite sizes and SpriteScale to reduce the distance
                var reducedDistance = MathF.Max(0, distance - 20);
                Set(follower, new SpriteScale(new Vector2(reducedDistance, 1f)));
            }
        }
    }
}
