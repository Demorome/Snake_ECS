#if DEBUG

using System;
using System.Collections.Generic;
using Hexa.NET.ImGui;
using MoonTools.ECS;
using RollAndCash.GameStates;
using RollAndCash.Systems;
using static RollAndCash.Systems.EditorSystem;

namespace RollAndCash.Editor;

public static class EditorHelpActions
{
    public class EditorAction
    {
        public EditorAction(string name, Action<World> action,
            bool opensWindow = false,
            Func<bool> isDisabledFunc = null)
        {
            Name = name;
            WorldAction = action;
            OpensWindow = opensWindow;
            MaybeIsDisabledFunc = isDisabledFunc;
        }
        public EditorAction(
            string name, 
            Action toggleAction, 
            Func<bool> getFunc,
            Func<bool> isDisabledFunc = null
            )
        {
            Name = name;
            MaybeToggleAction = toggleAction;
            MaybeGetFunc = getFunc;
            MaybeIsDisabledFunc = isDisabledFunc;
        }

        public string Name;
        public Action<World> WorldAction = null;
        public Action MaybeToggleAction = null;
        public Func<bool> MaybeGetFunc = null;
        public Func<bool> MaybeIsDisabledFunc = null;
        public bool OpensWindow = false;
        public bool ShowInEditWindow = false;

        public bool IsDisabled()
        {
            if (MaybeIsDisabledFunc != null && MaybeIsDisabledFunc())
            {
                return true;
            }
            return false;
        }

        public void Invoke(World world)
        {
            if (IsDisabled())
            {
                return;
            }

            if (WorldAction == null)
            {
                MaybeToggleAction();
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
            }
        }
    };

    public static Dictionary<ImGuiKey, EditorAction> EditorHelpKeybinds = new()
    {
        { ImGuiKey.MouseX1,              new("Toggle Freeze All",
            () => { GameplayState.FreezeTimeForAll = !GameplayState.FreezeTimeForAll; },
            () => GameplayState.FreezeTimeForAll)
        },
        { ImGuiKey.MouseX2,              new("Toggle Selection Mode",
            EditorSystem.ActiveTool.ToggleEntitySelection,  
            () => { return EditorSystem.ActiveTool.CurrentMode == ToolMode.EntitySelection; },
            () => { return !EditorTools.CanEnableEntitySelection(EditorSystem.ActiveTool.CurrentMode); })
        },
        { ImGuiKey.F1,                   new("Search By Component",
            DrawComponents.DrawComponentTypeSearch, true)
        },
        { ImGuiKey.F2,                   new("Lock Cursor Position",
            () => { GameplayState.LockingCursorPosition = !GameplayState.LockingCursorPosition; },
            () => GameplayState.LockingCursorPosition )
        },
        { ImGuiKey.ModCtrl | ImGuiKey.T, new("Show Colliders",
            () => { Renderer.DrawDebugColliders = !Renderer.DrawDebugColliders; },
            () => Renderer.DrawDebugColliders)
        },
        { ImGuiKey.ModCtrl | ImGuiKey.E, new("Toggle Level Editor",
            () => { LevelEditorManipulator.IsInLevelEditor = !LevelEditorManipulator.IsInLevelEditor; },
            () => LevelEditorManipulator.IsInLevelEditor )
        },
        { ImGuiKey.F3,                   new("Show Position Info",
            () => { IsShowingPositionInfo = !IsShowingPositionInfo; },
            () => IsShowingPositionInfo )
        },
        { ImGuiKey.F4,                   new("Show Camera Info",
            () => { IsShowingCameraInfo = !IsShowingCameraInfo; },
            () => IsShowingCameraInfo )
        },
    };

    public static Dictionary<ImGuiKey, EditorAction> EditorEditKeybinds = new()
    {
        { ImGuiKey.ModCtrl | ImGuiKey.Z, new("Undo", 
            UndoRedo.UndoLastChange, 
            false,
            () => !UndoRedo.HasChangesToUndo()) 
        },
        { ImGuiKey.ModCtrl | ImGuiKey.Y, new("Redo", 
            UndoRedo.RedoLastChange, 
            false,
            () => !UndoRedo.HasChangesToRedo()) 
        },
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

    static public string KeyComboToString(ImGuiKey keyChordCombo)
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
                bool disabled = false;
                if (namedAction.MaybeIsDisabledFunc != null && namedAction.MaybeIsDisabledFunc())
                {
                    disabled = true;
                    ImGui.BeginDisabled();
                }
                if (ImGui.SmallButton(namedAction.Name))
                {
                    namedAction.Invoke(world);
                }
                if (namedAction.MaybeToggleAction != null 
                    && namedAction.MaybeGetFunc != null)
                {
                    bool isChecked = namedAction.MaybeGetFunc();
                    ImGui.SameLine();

                    // Style manipulation is so we can shrink the checkbox; 
                    // PushStyleVar would force us to change X padding too.
                    var style = ImGui.GetStyle();
                    var oldYFramePadding = style.FramePadding.Y;
                    style.FramePadding.Y = 0.0f;
                    if (ImGui.Checkbox($"##{namedAction.Name}Toggle", ref isChecked))
                    {
                        namedAction.MaybeToggleAction();
                    }
                    style.FramePadding.Y = oldYFramePadding;
                }
                if (disabled)
                {
                    ImGui.EndDisabled();
                }
            }
            ImGui.EndTable();
        }

        ImGui.End();
    }
}

#endif