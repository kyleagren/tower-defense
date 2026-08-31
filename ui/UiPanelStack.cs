using Godot;
using System.Collections.Generic;

// Tracks which modal UI panels (build menu, inventory, ...) are open so they can stack -
// most-recently-opened draws on top and is the only one that responds to ui_cancel. Mouse
// capture is centralized here too, since it should stay released as long as anything is open.
public static class UiPanelStack
{
    private static readonly List<Control> _open = new();

    public static void Push(Control panel)
    {
        _open.Remove(panel);
        _open.Add(panel);
        ReassignZIndices();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public static void Pop(Control panel)
    {
        _open.Remove(panel);
        ReassignZIndices();

        if (_open.Count == 0)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public static bool IsTop(Control panel)
    {
        return _open.Count > 0 && _open[^1] == panel;
    }

    private static void ReassignZIndices()
    {
        for (int i = 0; i < _open.Count; i++)
        {
            _open[i].ZAsRelative = false;
            _open[i].ZIndex = i;
        }
    }
}
