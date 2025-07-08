#if DEBUG

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.AsyncIO;
using MoonWorks.Graphics;
using MoonWorks.Input;
using RollAndCash.Components;
using SDL3;
using Buffer = MoonWorks.Graphics.Buffer;

namespace RollAndCash;

public static class ImGuiHandler
{
    public static string EntityToString(World world, Entity e)
    {
        var tag = world.GetTag(e);
        if (tag.Length == 0)
        {
            return e.ToString();
        }
        return $"Entity {{ ID = {e.ID}, Tag = {tag} }}";
    }

    public static void DrawEntitiesAndComponents(World world)
    {
        ImGui.SetNextWindowSize(new Vector2(500, 500), ImGuiCond.Once);
        ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Once);

        ImGui.Begin("Entities (with positions)");

        if (ImGui.TreeNode("World"))
        {
            foreach (var entity in world.Debug_GetEntities(typeof(Position2D)))
            {
                if (!ImGui.TreeNode(EntityToString(world, entity)))
                {
                    continue;
                }

                foreach (var type in world.Debug_GetAllComponentTypes(entity))
                {
                    DrawComponentInspector(world, entity, type);
                }

                ImGui.TreePop();
            }

            ImGui.TreePop();
        }

        ImGui.End();
    }

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
        //{ typeof(Lives), DrawLives },
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
            var expanded = ImGui.CollapsingHeader(type.ToString());
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
        var input = direction.Value;

        if (ImGui.InputFloat2("Direction2D", ref input))
        {
            world.Set(entity, new SpriteScale(input));
        }
    }
    
    private static void DrawColorBlend(World world, Entity entity)
    {
        var color = world.Get<ColorBlend>(entity);
        var input = color.Color.ToVector4();

        if (ImGui.InputFloat4("ColorBlend", ref input))
        {
            world.Set(entity, new ColorBlend(new Color(input)));
        }
    }
}
#endif