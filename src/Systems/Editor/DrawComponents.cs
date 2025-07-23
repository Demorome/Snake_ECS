#if DEBUG

using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash.Components;
using RollAndCash.Systems;
using RollAndCash.Utility;

namespace RollAndCash.Editor;

public static class DrawComponents
{
    static HashSet<Type> ComponentTypeWindows = new();

    unsafe static ImGuiTextFilterPtr TypeSearchFilter = new(ImGuiNative.ImGuiTextFilter_ImGuiTextFilter(null));

    public static void DrawComponentTypeSearch(World world)
    {
        TypeSearchFilter.Draw("Search");

        for (int i = 0; i < EditorSystem.ComponentTypes.Count; ++i)
        {
            var type = EditorSystem.ComponentTypes[i];

            if (TypeSearchFilter.PassFilter(type.Name))
            {
                if (ImGui.Selectable(type.Name))
                {
                    ComponentTypeWindows.Add(type);
                }
            }
        }
    }

    public static void DrawEntitiesWithComponentWindows(World world)
    {
        foreach (var componentType in ComponentTypeWindows)
        {
            bool dontCloseWindow = true;
            ImGui.Begin($"Entities with {componentType.Name}", ref dontCloseWindow, ImGuiWindowFlags.AlwaysAutoResize);

            foreach (var entity in world.Debug_GetEntities(componentType))
            {
                // Don't want to spam debugger with boring/irrelevant entities.
                if (world.Has<Editor_DontShowInLists>(entity))
                {
                    continue;
                }

                var entityStr = EditorSystem.EntityToString(world, entity);
                bool treeIsShown = ImGui.TreeNode(entityStr);

                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    EditorSystem.DetachedWindows.TryAdd(entityStr, entity);
                }

                if (!treeIsShown)
                {
                    continue;
                }

                foreach (var type in world.Debug_GetAllComponentTypes(entity))
                {
                    DrawComponentInspector(world, entity, type);
                }

                ImGui.TreePop();
            }

            ImGui.End();
            if (!dontCloseWindow)
            {
                ComponentTypeWindows.Remove(componentType);
            }
        }
    }

    #region Draw Components

    delegate void DrawComponentAction(World world, Entity entity, ref bool changed);

    // Credits to @cosmonaut: https://discord.com/channels/571020752904519693/591369371369209871/1298383364813881385
    static Dictionary<Type, DrawComponentAction> ComponentTypeToInspectorAction = new()
    {
        { typeof(Position2D), DrawPosition2D },
        { typeof(SpriteScale), DrawSpriteScale },
        { typeof(Direction2D), DrawDirection2D },
        { typeof(Speed), DrawSpeed },
        //{ typeof(LevelBoundaries), DrawLevelBoundariesParameters },
        //{ typeof(SpriteAnimation), DrawSpriteAnimation },
        //{ typeof(Text), DrawText },
        { typeof(Angle), DrawAngle },
        { typeof(HasHealth), DrawHealth },
        { typeof(ColorBlend), DrawColorBlend },
        { typeof(Rectangle), DrawRectangle },
        { typeof(Depth), DrawDepth },
    };

    static Dictionary<Type, Func<Entity, string>> ComponentTypeToInspectorString = new()
    {

    };

    static dynamic ComponentPriorToChange_Cached = null;

    // Credits to @cosmonaut: https://discord.com/channels/571020752904519693/591369371369209871/1298383364813881385
    public static void DrawComponentInspector(World world, Entity entity, Type type)
    {
        if (ComponentTypeToInspectorAction.ContainsKey(type))
        {
            var expanded = ImGui.CollapsingHeader(type.Name);
            if (expanded)
            {
                var dummyComponent = (dynamic)Activator.CreateInstance(type);
                var componentPriorToChange = DynamicComponentManip.Get(world, entity, dummyComponent);
                // FIXME: Destroy dummyComponent? Profile if it leaks mem.

                bool doingChanges = false;
                ComponentTypeToInspectorAction[type].Invoke(world, entity, ref doingChanges);

                // Store quick-succession changes as a single change, for the 'Undo' feature. 
                if (ComponentPriorToChange_Cached == null)
                {
                    if (doingChanges)
                    {
                        ComponentPriorToChange_Cached = componentPriorToChange;
                        UndoRedo.ClearRedoList();
                    }
                }
                else if (!doingChanges && !ImGui.IsAnyItemActive())
                {
                    UndoRedo.ChangeHistory.Push((entity, ComponentPriorToChange_Cached, true));
                    Logger.LogInfo($"Stored prior state for {EditorSystem.EntityToString(world, entity)}'s {ComponentPriorToChange_Cached.GetType()}: {ComponentPriorToChange_Cached}");
                    ComponentPriorToChange_Cached = null;
                }
            }
        }
        else if (ComponentTypeToInspectorString.ContainsKey(type))
        {
            ImGui.Text($"{type}: {ComponentTypeToInspectorString[type].Invoke(entity)}");
        }
        else
        {
            ImGui.Text(type.ToString());
        }
    }

    private static void DrawSpeed(World world, Entity entity, ref bool changed)
    {
        var velocity = world.Get<Speed>(entity);
        var inputVelocity = velocity.Value;

        // NOTE: Without a space or ## in this tag, we can't input anything! Weird bug.
        // Probably because the label ID is used elsewhere, but hmm.
        if (ImGui.InputFloat("##Speed", ref inputVelocity))
        {
            world.Set(entity, new Speed(inputVelocity));
            changed = true;
        }
    }

    private static void DrawRectangle(World world, Entity entity, ref bool changed)
    {
        var rect = world.Get<Rectangle>(entity);
        var inputPosOffset = new Vector2(rect.X, rect.Y);

        if (ImGui.DragFloat2("Offset", ref inputPosOffset))
        {
            world.Set(entity, new Rectangle((int)inputPosOffset.X, (int)inputPosOffset.Y, rect.Width, rect.Height));
            changed = true;
        }

        var inputSize = new Vector2(rect.Width, rect.Height);
        if (ImGui.DragFloat2("Width/Height", ref inputSize))
        {
            world.Set(entity, new Rectangle(rect.X, rect.Y, (int)inputSize.X, (int)inputSize.Y));
            changed = true;
        }
    }

    private static void DrawPosition2D(World world, Entity entity, ref bool changed)
    {
        var pos = world.Get<Position2D>(entity);
        var input = pos.AsVector();

        if (ImGui.DragFloat2("Position2D", ref input))
        {
            world.Set(entity, new Position2D(input));
            changed = true;
        }

        // Credits to @rokups for this trick: https://github.com/ocornut/imgui/discussions/3848
        // And credits to Samurai Gunn 2 behind-the-scenes vids for the idea.
        ImGui.Button("Grab");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Click and drag to adjust the position.");
            ImGui.EndTooltip();
        }
        if (ImGui.IsItemActive())
        {
            pos += ImGui.GetIO().MouseDelta;
            world.Set(entity, pos);
            changed = true;
        }
    }

    static bool UniformScaleStretch = true;

    private static void DrawSpriteScale(World world, Entity entity, ref bool changed)
    {
        var scale = world.Get<SpriteScale>(entity);

        ImGui.Checkbox("Uniform scale?", ref UniformScaleStretch);

        if (UniformScaleStretch)
        {
            var input = scale.Scale.Y;
            ImGui.Text(scale.Scale.X.ToString());
            ImGui.SameLine();
            if (ImGui.DragFloat("Scale", ref input))
            {
                var newScale = new Vector2(input, input);
                world.Set(entity, new SpriteScale(newScale));
                changed = true;
            }
        }
        else
        {
            var input = scale.Scale;
            if (ImGui.DragFloat2("Scale", ref input))
            {
                world.Set(entity, new SpriteScale(input));
                changed = true;
            }
        }
    }

    private static void DrawDirection2D(World world, Entity entity, ref bool changed)
    {
        var direction = world.Get<Direction2D>(entity);
        var input = float.RadiansToDegrees(MathUtilities.AngleFromUnitVector(direction.Value));

        if (ImGui.InputFloat("Angle (degrees)", ref input))
        {
            var output = MathUtilities.UnitVectorFromAngle(float.DegreesToRadians(input));
            world.Set(entity, new Direction2D(output));
            changed = true;
        }

        if (ImGui.SliderFloat("Slider", ref input, -360f, 360))
        {
            var output = MathUtilities.UnitVectorFromAngle(float.DegreesToRadians(input));
            world.Set(entity, new Direction2D(output));
            changed = true;
        }
    }

    private static void DrawAngle(World world, Entity entity, ref bool changed)
    {
        var angle = world.Get<Angle>(entity);
        var input = float.RadiansToDegrees(angle.Value);

        if (ImGui.InputFloat("Angle (degrees)", ref input))
        {
            var output = float.DegreesToRadians(input);
            world.Set(entity, new Angle(output));
            changed = true;
        }

        if (ImGui.SliderFloat("Slider", ref input, -360f, 360))
        {
            var output = float.DegreesToRadians(input);
            world.Set(entity, new Angle(output));
            changed = true;
        }
    }

    private static void DrawColorBlend(World world, Entity entity, ref bool changed)
    {
        var color = world.Get<ColorBlend>(entity);
        var input = color.Color.ToVector4();

        if (ImGui.ColorEdit4("Color", ref input))
        {
            var output = new Color(input);
            world.Set(entity, new ColorBlend(output));
            changed = true;
        }
    }

    private static void DrawHealth(World world, Entity entity, ref bool changed)
    {
        var health = world.Get<HasHealth>(entity);
        var input = health.Health;

        if (ImGui.InputInt("Health", ref input))
        {
            world.Set(entity, new HasHealth(input));
            changed = true;
        }
    }

    private static void DrawDepth(World world, Entity entity, ref bool changed)
    {
        var depth = world.Get<Depth>(entity);
        var input = depth.Value;

        if (ImGui.InputFloat("##Depth", ref input))
        {
            world.Set(entity, new Depth(input));
            changed = true;
        }
    }
    #endregion
}

#endif