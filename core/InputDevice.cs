using Godot;
using System;

// Which kind of device the player last touched - what movement, the UI and button prompts adapt
// to. Reads in _Input, ahead of GUI and gameplay handlers, so a consumed event still counts.
public partial class InputDevice : Node
{
    private const float StickThreshold = 0.5f;
    private const float MouseMotionThreshold = 4f;

    public static bool UsingController { get; private set; }

    public static event Action Changed;

    public override void _Input(InputEvent @event)
    {
        bool? controller = @event switch
        {
            InputEventJoypadButton => true,
            InputEventJoypadMotion stick when Mathf.Abs(stick.AxisValue) >= StickThreshold => true,
            InputEventKey or InputEventMouseButton => false,
            InputEventMouseMotion mouse when mouse.Relative.Length() >= MouseMotionThreshold => false,
            _ => null
        };

        if (controller.HasValue && controller.Value != UsingController)
        {
            UsingController = controller.Value;
            Changed?.Invoke();
        }
    }
}
