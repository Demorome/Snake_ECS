using MoonWorks.Graphics;
using RollAndCash.Systems;
using RollAndCash.Data;
using RollAndCash.Messages;
using System.Numerics;
using System;
using System.Text.Json.Serialization;
using RollAndCash.ComponentSerialization;

namespace RollAndCash.Components;

/// <summary>
/// Using this instead of Components.Singletons namespace,
/// since it's more convenient to just type out "Singletons.Something",
/// instead of "Components.Singletons.Something". <br/>
/// Plus, it more clearly enforces explicitly declaring 
/// if a component was declared as a singleton.
/// </summary>
public static class Singletons
{
    /*
    public readonly record struct ActiveLevelID(LevelID ID);
    public readonly record struct ActiveRoomID(LevelRoomID ID);*/

#if DEBUG
    public readonly record struct Editor_GlobalDebugEntity();
#endif
}