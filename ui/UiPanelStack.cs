using Godot;
using System;
using System.Collections.Generic;

// Tracks which modal UI panels (build menu, inventory, ...) are open so they can stack -
// most-recently-opened draws on top and is the only one that responds to ui_cancel. Mouse mode is
// centralized here too: captured during play, released while anything is open - unless the top
// panel asked to keep it captured, as a radial wheel does to read raw mouse movement.
public static class UiPanelStack
{
    private static readonly List<Control> _open = new();
    private static readonly Dictionary<Control, Action> _closers = new();
    private static readonly HashSet<Control> _capturingMouse = new();
    private static readonly HashSet<Control> _blockingMovement = new();

    // Holds for the whole session: InputDevice outlives every scene.
    static UiPanelStack()
    {
        InputDevice.Changed += ApplyMouseMode;
    }

    public static void Push(Control panel, Action close, bool capturesMouse = false, bool blocksMovement = false)
    {
        _open.Remove(panel);
        _open.Add(panel);
        _closers[panel] = close;

        if (capturesMouse)
        {
            _capturingMouse.Add(panel);
        }
        else
        {
            _capturingMouse.Remove(panel);
        }

        if (blocksMovement)
        {
            _blockingMovement.Add(panel);
        }
        else
        {
            _blockingMovement.Remove(panel);
        }

        ReassignZIndices();
        ApplyMouseMode();
    }

    public static void Pop(Control panel)
    {
        _open.Remove(panel);
        _closers.Remove(panel);
        _capturingMouse.Remove(panel);
        _blockingMovement.Remove(panel);
        ReassignZIndices();
        ApplyMouseMode();
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
        _capturingMouse.Clear();
        _blockingMovement.Clear();
        ApplyMouseMode();
    }

    // Captured for play and for a panel that asked for it; free while any other panel is open, but
    // hidden on a controller, where a cursor parked wherever the mouse was last left is just clutter.
    private static void ApplyMouseMode()
    {
        Input.MouseMode = _open.Count == 0 || _capturingMouse.Contains(_open[^1]) ? Input.MouseModeEnum.Captured
            : InputDevice.UsingController ? Input.MouseModeEnum.Hidden
            : Input.MouseModeEnum.Visible;
    }

    public static bool IsTop(Control panel)
    {
        return _open.Count > 0 && _open[^1] == panel;
    }

    public static bool AnyOpen => _open.Count > 0;

    public static Control Top => _open.Count > 0 ? _open[^1] : null;

    // Some panels stand the character still, keyboard included: one whose choice replaces the
    // character or the level, where there's nowhere worth walking to mid-choice, and the build menu.
    public static bool BlocksMovement => _blockingMovement.Count > 0;

    // A text field has focus, so the keyboard is typing rather than playing.
    public static bool IsTyping =>
        Engine.GetMainLoop() is SceneTree tree && tree.Root.GuiGetFocusOwner() is LineEdit or TextEdit;

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
