#if DEBUG

using System;
using System.Collections.Generic;
using Hexa.NET.ImGui;
using MoonTools.ECS;
using RollAndCash.GameStates;
using static RollAndCash.Systems.EditorSystem;

namespace RollAndCash.Editor;

public static class EditorHelpActions
{
    public class EditorAction
    {
        public EditorAction(string name, Action<World> action,
            bool opensWindow = false, bool drawsOwnWindow = false, Func<bool> isDisabledFunc = null)
        {
            WorldAction = action;
            Name = name;
            OpensWindow = opensWindow;
            IsDisabledFunc = isDisabledFunc;
        }
        public EditorAction(string name, Func<bool> func)
        {
            ToggleFunc = func;
            Name = name;
        }

        public string Name;
        public Action<World> WorldAction = null;
        public Func<bool> ToggleFunc = null;
        public Func<bool> IsDisabledFunc = null;
        public bool OpensWindow = false;
        public bool ShowInEditWindow = false;

        public bool IsDisabled()
        {
            if (IsDisabledFunc != null && IsDisabledFunc())
            {
                return true;
            }
            return false;
        }

        public bool? Invoke(World world)
        {
            if (IsDisabled())
            {
                return false;
            }

            if (WorldAction == null)
            {
                return ToggleFunc();
            }
            else
            {
                if (OpensWindow)
                {
                    DetachedWindows.TryAdd(Name, WorldAction);
                }
                else
                {
                    WorldAction(world);
                }
                return null;
            }
        }
    };

    public static Dictionary<ImGuiKey, EditorAction> EditorHelpKeybinds = new()
    {
        { ImGuiKey.MouseX1,              new("Toggle Freeze All",
            () => { return GameplayState.FreezeTimeForAll = !GameplayState.FreezeTimeForAll; } )
        },
        { ImGuiKey.MouseX2,              new("Toggle Selection Mode",
             () => { return IsInEntitySelectionMode = !IsInEntitySelectionMode; } )
        },
        { ImGuiKey.F1,                   new("Search By Component",
            DrawComponents.DrawComponentTypeSearch, true)
        },
        { ImGuiKey.F2,                   new("Lock Cursor Position",
            () => { return GameplayState.LockingCursorPosition = !GameplayState.LockingCursorPosition; } )
        },
        { ImGuiKey.ModCtrl | ImGuiKey.T, new("Show Colliders",
            () => { return Renderer.DrawDebugColliders = !Renderer.DrawDebugColliders; } )
        },
        { ImGuiKey.ModCtrl | ImGuiKey.E, new("Toggle Level Editor",
            () => { return LevelEditorManipulator.IsInLevelEditor = !LevelEditorManipulator.IsInLevelEditor; } )
        },
    };

    public static Dictionary<ImGuiKey, EditorAction> EditorEditKeybinds = new()
    {
        { ImGuiKey.ModCtrl | ImGuiKey.Z, new("Undo", UndoRedo.UndoLastChange, false, false,
            () => !UndoRedo.HasChangesToUndo()) },
        { ImGuiKey.ModCtrl | ImGuiKey.Y, new("Redo", UndoRedo.RedoLastChange, false, false,
            () => !UndoRedo.HasChangesToRedo()) },
    };

    public static void HandleEditorKeybinds(World world)
    {
        foreach (var (key, editorAction) in EditorHelpKeybinds)
        {
            if (key != ImGuiKey.None && ImGui.IsKeyChordPressed((int)key))
            {
                editorAction.Invoke(world);
            }
        }

        foreach (var (key, editorAction) in EditorEditKeybinds)
        {
            if (key != ImGuiKey.None && ImGui.IsKeyChordPressed((int)key))
            {
                editorAction.Invoke(world);
            }
        }
    }

    static string KeyComboToString(ImGuiKey keyChordCombo)
    {
        var key = keyChordCombo & ~ImGuiKey.ModMask;
        var modKey = keyChordCombo & ImGuiKey.ModMask;
        // Remove first 3 chars to get rid of "Mod" prefix
        var modKeyStr = modKey != 0 ? modKey.ToString().Remove(0, 3) + "+" : "";

        return modKeyStr + (key != ImGuiKey.None ? key.ToString() : "");
    }

    public static void DrawHelpWindow(World world)
    {
        ImGui.Begin("Help", ImGuiWindowFlags.AlwaysAutoResize);

        var tableFlags = ImGuiTableFlags.BordersInnerV
            | ImGuiTableFlags.NoHostExtendX
            | ImGuiTableFlags.SizingFixedFit;

        if (ImGui.BeginTable("##Help_Table", 2, tableFlags))
        {
            foreach (var (keybind, namedAction) in EditorHelpKeybinds)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.Text(KeyComboToString(keybind));

                ImGui.TableNextColumn();
                if (ImGui.SmallButton(namedAction.Name))
                {
                    namedAction.Invoke(world);
                }
                if (namedAction.ToggleFunc != null)
                {
                    var isChecked = !namedAction.ToggleFunc();
                    namedAction.ToggleFunc(); // toggle it again to reset it to what it was (hacky, I know).
                    ImGui.SameLine();

                    // Style manipulation is so we can shrink the checkbox; 
                    // PushStyleVar would force us to change X padding too.
                    var style = ImGui.GetStyle();
                    var oldYFramePadding = style.FramePadding.Y;
                    style.FramePadding.Y = 0.0f;
                    if (ImGui.Checkbox($"##{namedAction.Name}Toggle", ref isChecked))
                    {
                        namedAction.ToggleFunc();
                    }
                    style.FramePadding.Y = oldYFramePadding;
                }
            }
            ImGui.EndTable();
        }

        ImGui.End();
    }

    public static void DrawWindowMenuBar(World world)
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Edit"))
            {
                foreach (var (keybind, editorAction) in EditorEditKeybinds)
                {
                    var isDisabled = editorAction.IsDisabled();
                    if (ImGui.MenuItem(editorAction.Name, KeyComboToString(keybind), false, !isDisabled))
                    {
                        editorAction.Invoke(world);
                    }
                }
                
                /*
                ImGui.Separator();
                if (ImGui::MenuItem("Cut", "CTRL+X")) { }
                if (ImGui::MenuItem("Copy", "CTRL+C")) {}
                if (ImGui::MenuItem("Paste", "CTRL+V")) {}*/
                ImGui.EndMenu();
            }
            ImGui.EndMainMenuBar();
        }
    }
}

#endif