#if DEBUG
//using static RollAndCash.Systems.EditorSystem;

using Hexa.NET.ImGui;
using MoonWorks;

namespace RollAndCash.Editor;

public enum ToolMode
{
    /// <summary>
    /// TODO: Control details.
    /// The default tool mode.
    /// </summary>
    LevelViewing = 0,

    /// <summary>
    /// Paint with LMB and erase with RMB.
    /// </summary>
    PaintingAndErasing,

    /// <summary>
    /// TODO: Control details.
    /// Can't enter this mode while painting/erasing.
    /// </summary>
    EntitySelection,

    DEFAULT = LevelViewing
}

public class EditorTools
{
    private ToolMode _CurrentMode = ToolMode.DEFAULT;
    public ToolMode CurrentMode 
    { 
        get => _CurrentMode; 
        private set
        {
            if (_CurrentMode != value)
            {
                LastMode = _CurrentMode;
                _CurrentMode = value;
            }
        } 
    }
    private ToolMode LastMode = ToolMode.DEFAULT;

    public void RevertToDefaultMode()
    {
        CurrentMode = ToolMode.DEFAULT;
    }

    public bool TrySetMode(ToolMode newMode)
    {
        bool success = true;

        if (newMode == ToolMode.EntitySelection
            && !CanEnableEntitySelection(CurrentMode))
        {
            success = false;
        }

        if (success)
        {
            CurrentMode = newMode;
        }
        else
        {
            Logger.LogWarn($"Couldn't change editor tool mode from {CurrentMode} to {newMode}");
        }

        return success;
    }

    public static bool CanEnableEntitySelection(ToolMode currentMode)
    {
        return currentMode != ToolMode.PaintingAndErasing;
    }

    public void ToggleEntitySelection()
    {
        if (CurrentMode == ToolMode.EntitySelection)
        {
            CurrentMode = LastMode;
        }
        else if (CanEnableEntitySelection(CurrentMode))
        {
            CurrentMode = ToolMode.EntitySelection;
        }
    }

    // FIXME: Use icons instead!
    public void ShowCurrentTool()
    {
        if (ImGui.Begin("Tool Mode"u8))
        {
            ImGui.Text(CurrentMode.ToString());
        }
        ImGui.End();
    }
}

#endif