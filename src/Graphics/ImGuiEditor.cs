#if DEBUG

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Unicode;
using ImGuiNET;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.AsyncIO;
using MoonWorks.Graphics;
using MoonWorks.Input;
using MoonWorks.Math;
using RollAndCash.Components;
using RollAndCash.Relations;
using RollAndCash.Utility;
using SDL3;
using Buffer = MoonWorks.Graphics.Buffer;

namespace RollAndCash;

public static class ImGuiEditor
{
    static List<Type> ComponentTypes = new();

    public static void Init()
    {
        // FIXME: Update on hot-reload, if we add new component types?
        InitComponentTypesList();
    }

    static void InitComponentTypesList()
    {
        string namespaceFilter = "RollAndCash.Components";

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (!type.IsValueType || type.Namespace != namespaceFilter)
            {
                continue;
            }

            ComponentTypes.Add(type);
        }

        ComponentTypes.Sort((Type A, Type B) => { return A.Name.CompareTo(B.Name); });
    }

    public static string EntityToString(World world, Entity e)
    {
        var tag = world.GetTag(e);
        if (tag.Length == 0)
        {
            return e.ToString();
        }
        return $"Entity {{ ID = {e.ID}, Tag = {tag} }}";
    }

    class DebugAction
    {
        public DebugAction(Action<World> action, string name, bool opensWindow = false)
        {
            Action = action;
            Name = name;
            OpensWindow = opensWindow;
        }

        public Action<World> Action;
        public string Name;
        public bool OpensWindow;
    };

    static Dictionary<ImGuiKey, DebugAction> DebugKeybinds = new()
    {
        { ImGuiKey.F1, new DebugAction(DrawComponentTypeSearch, "Search By Component", true)},
        { ImGuiKey.ModCtrl | ImGuiKey.E, new DebugAction(
            (World _) => { Renderer.DrawDebugColliders = !Renderer.DrawDebugColliders; },
            "Show Colliders")
        },

    };

    public static void DrawHelpWindow(World world)
    {
        ImGui.Begin("Help", ImGuiWindowFlags.AlwaysAutoResize);

        var tableFlags = ImGuiTableFlags.BordersInnerV
            | ImGuiTableFlags.NoHostExtendX
            | ImGuiTableFlags.SizingFixedFit;

        if (ImGui.BeginTable("##Help", 2, tableFlags))
        {
            foreach (var (requiredInput, namedAction) in DebugKeybinds)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var key = requiredInput & ~ImGuiKey.ModMask;
                var modKey = requiredInput & ImGuiKey.ModMask;
                // Remove first 3 chars to get rid of "Mod" prefix
                var modKeyStr = modKey != 0 ? modKey.ToString().Remove(0, 3) + "+" : "";
                ImGui.Text(modKeyStr + key.ToString());

                ImGui.TableNextColumn();
                ImGui.Text(namedAction.Name);

            }
            ImGui.EndTable();
        }

        ImGui.End();
    }

    public static void HandleDebugKeybinds(World world)
    {
        foreach (var (key, debugAction) in DebugKeybinds)
        {
            if (ImGui.IsKeyChordPressed(key))
            {
                if (debugAction.OpensWindow)
                {
                    DetachedWindows.TryAdd(debugAction.Name, debugAction.Action);
                }
                else
                {
                    debugAction.Action(world);
                }
            }
        }
    }

    static Dictionary<string, object> DetachedWindows = new();

    public static void DrawDetachedWindows(World world)
    {
        // Credits to @APurpleApple for this trick: https://discord.com/channels/571020752904519693/571020753479401483/1347847933709783102
        foreach (var (windowTitle, obj) in DetachedWindows)
        {
            bool dontCloseWindow = true;
            ImGui.Begin(windowTitle, ref dontCloseWindow);

            if (obj.GetType() == typeof(Entity))
            {
                var entity = (Entity)obj;
                foreach (var type in world.Debug_GetAllComponentTypes(entity))
                {
                    DrawComponentInspector(world, entity, type);
                }
            }
            else if (obj.GetType() == typeof(Action<World>))
            {
                var action = (Action<World>)obj;
                action(world);
            }

            ImGui.End();
            if (!dontCloseWindow)
            {
                DetachedWindows.Remove(windowTitle);
            }
        }
    }

    static HashSet<Type> ComponentTypeWindows = new();

    unsafe static ImGuiTextFilterPtr searchFilter = new(ImGuiNative.ImGuiTextFilter_ImGuiTextFilter(null));

    public static void DrawComponentTypeSearch(World world)
    {
        searchFilter.Draw("Search");

        for (int i = 0; i < ComponentTypes.Count; ++i)
        {
            var type = ComponentTypes[i];

            if (searchFilter.PassFilter(type.Name))
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
                // Don't want to spam debugger with boring entities.
                if (world.HasInRelation<DetectionVisualPoint>(entity))
                {
                    continue;
                }

                var entityStr = EntityToString(world, entity);
                if (!ImGui.TreeNode(entityStr))
                {
                    continue;
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    DetachedWindows.TryAdd(entityStr, entity);
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

    // Credits to @cosmonaut: https://discord.com/channels/571020752904519693/591369371369209871/1298383364813881385
    static Dictionary<Type, Action<World, Entity>> ComponentTypeToInspectorAction = new()
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
        { typeof(Rectangle), DrawRectangle }
    };

    static Dictionary<Type, Func<Entity, string>> ComponentTypeToInspectorString = new()
    {

    };

    // Credits to @cosmonaut: https://discord.com/channels/571020752904519693/591369371369209871/1298383364813881385
    private static void DrawComponentInspector(World world, Entity entity, Type type)
    {
        if (ComponentTypeToInspectorAction.ContainsKey(type))
        {
            var expanded = ImGui.CollapsingHeader(type.Name);
            if (expanded)
            {
                ComponentTypeToInspectorAction[type].Invoke(world, entity);
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

    private static void DrawSpeed(World world, Entity entity)
    {
        var velocity = world.Get<Speed>(entity);
        var inputVelocity = velocity.Value;

        if (ImGui.InputFloat("Speed", ref inputVelocity))
        {
            world.Set(entity, new Speed(inputVelocity));
        }
    }

    private static void DrawRectangle(World world, Entity entity)
    {
        var rect = world.Get<Rectangle>(entity);
        var inputPosOffset = new Vector2(rect.X, rect.Y);

        if (ImGui.InputFloat2("Offset", ref inputPosOffset))
        {
            world.Set(entity, new Rectangle((int)inputPosOffset.X, (int)inputPosOffset.Y, rect.Width, rect.Height));
        }

        var inputSize = new Vector2(rect.Width, rect.Height);
        if (ImGui.InputFloat2("Width/Height", ref inputSize))
        {
            world.Set(entity, new Rectangle(rect.X, rect.Y, (int)inputSize.X, (int)inputSize.Y));
        }
    }

    private static void DrawPosition2D(World world, Entity entity)
    {
        var pos = world.Get<Position2D>(entity);
        var input = pos.AsVector();

        if (ImGui.InputFloat2("Position2D", ref input))
        {
            world.Set(entity, new Position2D(input));
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
        }
    }

    private static void DrawSpriteScale(World world, Entity entity)
    {
        var scale = world.Get<SpriteScale>(entity);
        var input = scale.Scale;

        if (ImGui.InputFloat2("SpriteScale", ref input))
        {
            world.Set(entity, new SpriteScale(input));
        }
    }

    private static void DrawDirection2D(World world, Entity entity)
    {
        var direction = world.Get<Direction2D>(entity);
        var input = float.RadiansToDegrees(MathUtilities.AngleFromUnitVector(direction.Value));

        if (ImGui.InputFloat("Angle (degrees)", ref input))
        {
            var output = MathUtilities.UnitVectorFromAngle(float.DegreesToRadians(input));
            world.Set(entity, new Direction2D(output));
        }

        if (ImGui.SliderFloat("Slider", ref input, -360f, 360))
        {
            var output = MathUtilities.UnitVectorFromAngle(float.DegreesToRadians(input));
            world.Set(entity, new Direction2D(output));
        }
    }

    private static void DrawAngle(World world, Entity entity)
    {
        var angle = world.Get<Angle>(entity);
        var input = float.RadiansToDegrees(angle.Value);

        if (ImGui.InputFloat("Angle (degrees)", ref input))
        {
            var output = float.DegreesToRadians(input);
            world.Set(entity, new Angle(output));
        }

        if (ImGui.SliderFloat("Slider", ref input, -360f, 360))
        {
            var output = float.DegreesToRadians(input);
            world.Set(entity, new Angle(output));
        }
    }

    private static void DrawColorBlend(World world, Entity entity)
    {
        var color = world.Get<ColorBlend>(entity);
        var input = color.Color.ToVector4();

        if (ImGui.ColorEdit4("Color", ref input))
        {
            world.Set(entity, new ColorBlend(new Color(input)));
        }
    }

    private static void DrawHealth(World world, Entity entity)
    {
        var health = world.Get<HasHealth>(entity);
        var input = health.Health;

        if (ImGui.InputInt("Health", ref input))
        {
            world.Set(entity, new HasHealth(input));
        }
    }
    #endregion Draw Components

}

#endif