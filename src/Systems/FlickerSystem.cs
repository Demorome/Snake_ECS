using System;
using MoonTools.ECS;
using RollAndCash.Components;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

namespace RollAndCash.Systems;

// Credits to Cassandra Lugo's tutorial: https://blood.church/posts/2023-09-25-shmup-tutorial/
// Slightly modified to use Relations() instead of a Filter that loops through all sprite-having entities.
public class FlickerSystem : MoonTools.ECS.System
{
    FlickeringManipulator FlickeringManipulator;

    public FlickerSystem(World world) : base(world)
    {
        FlickeringManipulator = new FlickeringManipulator(world);
    }

    public override void Update(TimeSpan delta)
    {
        var deltaTime = (float)delta.TotalSeconds;

        foreach (var message in ReadMessages<StartFlickering>())
        {
            FlickeringManipulator.StartFlickering(message.Target, message.TotalTime, message.FlickerTime);
        }

        foreach (var (entity, flickeringTimerEntity) in Relations<Relations.WillFlicker>())
        {
            var flickeringTimer = Get<Timer>(flickeringTimerEntity);
            var flickeringData = GetRelationData<WillFlicker>(entity, flickeringTimerEntity);

            if (TimeUtilities.OnTime(
                flickeringTimer.Time, 
                flickeringData.TimePerFlicker, 
                flickeringData.TimePerFlicker + deltaTime, 
                flickeringData.TimePerFlicker * 2)
                )
            {
                Relate(entity, flickeringTimerEntity, new DontDraw());
            }
            else
            {
                Unrelate<DontDraw>(entity, flickeringTimerEntity);
            }
            
            /*
            if (HasOutRelation<Relations.Flicker>(entity))
            {
                var timer = OutRelationSingleton<Relations.Flicker>(entity);
                var time = Get<Timer>(timer);
                if (time.Time > time.Max * 0.5f)
                {
                    Relate(entity, flickeringTimerEntity, new Invisible());
                }
                else
                {
                    Unrelate<Invisible>(entity, flickeringTimerEntity);
                }
            }
            else
            {
                var timer = CreateEntity();
                Set(timer, new Timer(flickering.TimePerFlicker));
                Relate(entity, timer, new Relations.Flicker());
            }*/
        }
    }
}