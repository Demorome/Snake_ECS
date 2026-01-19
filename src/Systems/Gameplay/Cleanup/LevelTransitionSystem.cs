using System;
using MoonTools.ECS;
using RollAndCash.Relations;
using RollAndCash.Components;

namespace RollAndCash.Systems;

public class LevelTransitionSystem : MoonTools.ECS.System
{
    private Filter DestroyOnTransitionFilter;

    public LevelTransitionSystem(World world) : base(world)
    {
        DestroyOnTransitionFilter 
            = FilterBuilder
            .Include<DestroyOnTransition>()
            // Avoid destroying entities that are loaded in an adjacent cell
            .Exclude<Disabled>()
            .Build();
    }

    public override void Update(TimeSpan delta)
    {
        // TODO: More fancy code, to support camera panning over to the new room.
        // Once the transition is over, finally destroy the entities.
        
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