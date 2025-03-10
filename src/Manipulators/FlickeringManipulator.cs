using System;
using System.ComponentModel;
using MoonTools.ECS;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class FlickeringManipulator : MoonTools.ECS.Manipulator
{
    public FlickeringManipulator(World world) : base(world)
    {
    }

    public void StartFlickering(Entity target, float totalTime, float flickerTime, bool repeats = false)
    {
        if (HasOutRelation<WillFlicker>(target))
        {
            return;
        }

        var timer = CreateEntity();
        Set(timer, new Timer(totalTime, repeats));
        Relate(target, timer, new WillFlicker(flickerTime));
    }
}