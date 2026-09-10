using Godot;
using System;
using System.Collections.Generic;

// Tracks which modal UI panels (build menu, inventory, ...) are open so they can stack -
// most-recently-opened draws on top and is the only one that responds to ui_cancel. Mouse
// capture is centralized here too, since it should stay released as long as anything is open.
public static class UiPanelStack
{
    private static readonly List<Control> _open = new();
    private static readonly Dictionary<Control, Action> _closers = new();

    public static void Push(Control panel, Action close)
    {
        _open.Remove(panel);
        _open.Add(panel);
        _closers[panel] = close;
        ReassignZIndices();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public static void Pop(Control panel)
    {
        _open.Remove(panel);
        _closers.Remove(panel);
        ReassignZIndices();

        if (_open.Count == 0)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    // Closes every currently-open panel in one Escape press instead of requiring one press per
    // layered panel. Snapshots _open first - each closer calls Pop, which would otherwise mutate
    // the list this loop is still iterating.
    public static void CloseAll()
    {
        foreach (Control panel in _open.ToArray())
        {
            if (_closers.TryGetValue(panel, out Action close))
            {
                close();
            }
        }
    }

    public static void Reset()
    {
        _open.Clear();
        _closers.Clear();
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public static bool IsTop(Control panel)
    {
        return _open.Count > 0 && _open[^1] == panel;
    }

    public static bool AnyOpen => _open.Count > 0;

    // Base offset keeps a stacked panel above ordinary always-on HUD content (health orb, shield
    // bar, token bar, ...), which sits at the default z_index 0 and would otherwise win by tree
    // order alone against a panel like BuildMenu that happens to live earlier in the tree.
    private const int ZIndexBase = 100;

    private static void ReassignZIndices()
    {
        for (int i = 0; i < _open.Count; i++)
        {
            _open[i].ZAsRelative = false;
            _open[i].ZIndex = ZIndexBase + i;
        }
    }
}
