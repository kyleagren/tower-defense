using Godot;

// Holding a direction on a controller keeps stepping focus through an open panel after a short
// delay - the OS repeats held keyboard keys, but nothing repeats controller input. Steps through
// FindValidFocusNeighbor rather than synthesizing ui_* events, so Input's own record of what's held
// is never disturbed, and explicit focus neighbors apply exactly as they do for a real press.
public partial class UiNavigationRepeat : Node
{
    private const float InitialDelay = 0.3f;
    private const float RepeatInterval = 0.1f;

    private static readonly (string Action, Side Side)[] _directions =
    {
        ("ui_left", Side.Left),
        ("ui_right", Side.Right),
        ("ui_up", Side.Top),
        ("ui_down", Side.Bottom)
    };

    private int _heldDirection = -1;
    private float _timeUntilRepeat;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        int direction = InputDevice.UsingController && UiPanelStack.AnyOpen ? StrongestDirection() : -1;

        // The first step of a fresh press is Godot's own; this only takes over once it's held.
        if (direction != _heldDirection)
        {
            _heldDirection = direction;
            _timeUntilRepeat = InitialDelay;
            return;
        }

        if (direction < 0)
        {
            return;
        }

        _timeUntilRepeat -= (float)delta;

        if (_timeUntilRepeat > 0f)
        {
            return;
        }

        _timeUntilRepeat += RepeatInterval;

        if (GetViewport().GuiGetFocusOwner() is Control focused)
        {
            focused.FindValidFocusNeighbor(_directions[direction].Side)?.GrabFocus();
        }
    }

    // A diagonal on the stick picks whichever axis is pushed further.
    private static int StrongestDirection()
    {
        int strongest = -1;
        float strongestValue = 0f;

        for (int i = 0; i < _directions.Length; i++)
        {
            float strength = Input.GetActionStrength(_directions[i].Action);

            if (strength > strongestValue)
            {
                strongest = i;
                strongestValue = strength;
            }
        }

        return strongest;
    }
}
