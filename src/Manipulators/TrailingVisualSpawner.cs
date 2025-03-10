using System;
using System.ComponentModel;
using MoonTools.ECS;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Content;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;

public class TrailingVisualSpawner : MoonTools.ECS.Manipulator
{
    public TrailingVisualSpawner(World world) : base(world)
    {
    }

    public Entity CreateTrailingVisual(
        Entity source
        )
    {
        var sourcePos = Get<Position>(source);
        var sourceAnim = Get<SpriteAnimation>(source);
        var color = Has<ColorBlend>(source) ? Get<ColorBlend>(source).Color : Color.White;
        color.A /= 2; // transparency

        var trailingVisual = CreateEntity();
        Set(trailingVisual, sourcePos);
        Set(trailingVisual, sourceAnim);
        Set(trailingVisual, new ColorBlend(color));

        Relate(source, trailingVisual, new TrailingVisuals());
        Relate(trailingVisual, source, new Source());
        Set(trailingVisual, new DestroyWhenNoSource());

        return trailingVisual;
    }
}