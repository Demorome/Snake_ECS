using System;
using MoonTools.ECS;
using RollAndCash.Relations;
using RollAndCash.Components;
using Timed = RollAndCash.Components.Timed;

namespace RollAndCash.Systems;

public class LevelTransitionSystem : MoonTools.ECS.System
{
    private Filter DestroyOnTransitionFilter;

    public LevelTransitionSystem(World world) : base(world)
    {
        DestroyOnTransitionFilter 
            = FilterBuilder
            .Include<DestroyOnTransition>()
            .Build();
    }

    public override void Update(TimeSpan delta)
    {
        /*
        if (!SomeMessage<LevelTransitionMessage>())
        {
            return;
        }
        var levelTransitionInfo = ReadMessage<LevelTransitionMessage>();

        foreach (var entity in DestroyOnTransitionFilter.Entities)
        {
            
        }*/
    }
}