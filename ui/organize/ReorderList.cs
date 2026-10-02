using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// A list the player reorders by hand, the same way on mouse, keyboard and controller: press an entry
// to pick it up, move it with up/down or the mouse wheel, and press it again to put it down. Clicking
// another entry while holding one puts it down there. Each entry is a row, so the owner can put action
// buttons beside it. Reports a move only once the entry is put down somewhere new; the owner applies
// it to its own data.
public partial class ReorderList : VBoxContainer
{
    public event Action<int, int> Moved;

    private static readonly Color _heldTint = new Color(1f, 0.85f, 0.4f);

    private Control _held;
    private int _heldFrom;

    public bool IsHolding => _held != null;

    // One row per label: the entry that's picked up and moved, then whatever addExtras puts beside it,
    // called with the row's index. Entries pass mouse input on, so the wheel reaches this list (and a
    // ScrollContainer around it) from over any of them.
    public void SetEntries(IEnumerable<string> labels, Action<int, HBoxContainer> addExtras = null)
    {
        CancelHold();

        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        int index = 0;

        foreach (string label in labels)
        {
            HBoxContainer row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            Button entry = new Button
            {
                Text = label,
                Alignment = HorizontalAlignment.Left,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Pass
            };
            entry.Pressed += () => OnEntryPressed(row);
            row.AddChild(entry);

            addExtras?.Invoke(index++, row);
            AddChild(row);
        }
    }

    public void FocusEntry(int index)
    {
        if (index >= 0 && index < GetChildCount())
        {
            EntryOf(GetChild<Control>(index)).GrabFocus();
        }
    }

    public void FocusFirst() => FocusEntry(0);

    // Puts a held entry back where it was picked up.
    public void CancelHold()
    {
        if (_held == null)
        {
            return;
        }

        MoveChild(_held, _heldFrom);
        Release();
    }

    private static Button EntryOf(Control row) => row.GetChild<Button>(0);

    private void OnEntryPressed(Control row)
    {
        if (_held == null)
        {
            PickUp(row);
            return;
        }

        if (row != _held)
        {
            MoveChild(_held, row.GetIndex());
        }

        PutDown();
    }

    private void PickUp(Control row)
    {
        _held = row;
        _heldFrom = row.GetIndex();
        row.Modulate = _heldTint;
        SetHolding(row);
    }

    private void PutDown()
    {
        Control row = _held;
        int from = _heldFrom;
        int to = row.GetIndex();

        Release();
        EntryOf(row).GrabFocus();

        if (to != from)
        {
            Moved?.Invoke(from, to);
        }
    }

    private void Release()
    {
        _held.Modulate = Colors.White;
        _held = null;
        SetHolding(null);
    }

    // While an entry is held nothing else can take focus, so up/down move it instead of moving off it
    // - including UiNavigationRepeat's held-direction stepping, which finds no neighbor to go to. The
    // other entries stay clickable, as places to put it down; the rows' action buttons go inert.
    private void SetHolding(Control held)
    {
        foreach (Control row in GetChildren().Cast<Control>())
        {
            Button entry = EntryOf(row);
            entry.FocusMode = held == null || row == held ? FocusModeEnum.All : FocusModeEnum.None;

            foreach (BaseButton action in row.GetChildren().OfType<BaseButton>().Where(button => button != entry))
            {
                action.FocusMode = held == null ? FocusModeEnum.All : FocusModeEnum.None;
                action.MouseFilter = held == null ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;
            }
        }
    }

    // Reached by input on any entry, which passes it up here unhandled: up/down from the focused
    // held entry, or the wheel over whichever row the mouse is on.
    public override void _GuiInput(InputEvent @event)
    {
        if (_held == null)
        {
            return;
        }

        int step = @event.IsActionPressed("ui_up", allowEcho: true) || @event is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } ? -1
            : @event.IsActionPressed("ui_down", allowEcho: true) || @event is InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } ? 1
            : 0;

        if (step == 0)
        {
            return;
        }

        MoveChild(_held, Mathf.Clamp(_held.GetIndex() + step, 0, GetChildCount() - 1));
        AcceptEvent();
    }
}
