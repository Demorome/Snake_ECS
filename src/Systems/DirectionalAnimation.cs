
using System;
using System.Numerics;
using MoonTools.ECS;
using Snake.Components;
using Snake.Data;
using Snake.Messages;

namespace Snake.Systems;

public class DirectionalAnimation : MoonTools.ECS.System
{
    MoonTools.ECS.Filter DirectionFilter;
    public DirectionalAnimation(World world) : base(world)
    {
        DirectionFilter = FilterBuilder
        .Include<LastMovedDirection>()
        .Include<DirectionalSprites>()
        .Build();
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var entity in DirectionFilter.Entities)
        {
            var direction = Get<LastMovedDirection>(entity).Direction;
            var animations = Get<DirectionalSprites>(entity);

            SpriteAnimationInfo animation;

            if (direction.X > 0)
            {
                animation = SpriteAnimationInfo.FromID(animations.Right);
            }
            else if (direction.X < 0)
            {
                animation = SpriteAnimationInfo.FromID(animations.Left);
            }
            else
            {
                if (direction.Y > 0)
                {
                    animation = SpriteAnimationInfo.FromID(animations.Down);
                }
                else if (direction.Y < 0)
                {
                    animation = SpriteAnimationInfo.FromID(animations.Up);
                }
                else
                {
                    animation = Get<SpriteAnimation>(entity).SpriteAnimationInfo;
                }
            }

            int framerate = Get<SpriteAnimation>(entity).FrameRate;

            if (Has<AdjustFramerateToSpeed>(entity))
            {
                //var velocity = Has<IntegerVelocity>(entity) ? Get<IntegerVelocity>(entity).Value : Vector2.Zero;
                var moveTimerMax = Has<MovementTimer>(entity) ? Get<MovementTimer>(entity).Max : 1f;
                //framerate = (int)(velocity.Length() / moveTimerMax);
                framerate = (int)((1 / moveTimerMax) * 2f);
            }

            if (direction.LengthSquared() > 0)
            {
                Send(new SetAnimationMessage(
                    entity,
                    new SpriteAnimation(animation, framerate, true)
                ));
            }
            else
            {
                framerate = 0;
                Send(new SetAnimationMessage(
                    entity,
                    new SpriteAnimation(animation, framerate, true, 0),
                    true
                ));
            }
        }
    }

}
