using MoonWorks.Graphics;
using RollAndCash.Systems;
using RollAndCash.Data;
using RollAndCash.Messages;
using System.Numerics;
using System;
using System.Text.Json.Serialization;
using RollAndCash.ComponentSerialization;

namespace RollAndCash.Components.Singletons;



#if DEBUG
    public readonly record struct Editor_GlobalDebugEntity();
#endif