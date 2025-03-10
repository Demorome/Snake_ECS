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
        foreach (var (follower, target) in Relations<Following>())
        {
            if (HasOutRelation<DontFollowTarget>(follower))
            {
                continue;
            }

            var followData = GetRelationData<Following>(follower, target);

            var followerPos = Get<Position>(follower);
            var targetPos = Get<Position>(target);
            var distance = Vector2.Distance(targetPos.AsVector(), followerPos.AsVector());

            if (followData.MatchPosition)
            {
                Set(follower, targetPos);
            }

            if (followData.LookTowards)
            {
                var orientation = MathF.Atan2(targetPos.Y - followerPos.Y, targetPos.X - followerPos.X);
                Set(follower, new Angle(orientation));
            }

            if (followData.StretchTowards)
            {
                var reducedDistance = MathF.Max(0, distance - 20);
                Set(follower, new SpriteScale(new Vector2(reducedDistance, 1f)));
            }
        }
    }
}
