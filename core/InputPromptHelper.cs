using Godot;
using System.Linq;

// Names the binding the player would actually press right now - the keyboard or mouse one while
// they're on keyboard, the controller one on a controller, labeled for that controller's family.
// Text throughout; ButtonGlyph draws the few that read better as shapes.
public static class InputPromptHelper
{
    private enum ControllerFamily { Xbox, PlayStation }

    public static string GetPrompt(string action)
    {
        return GetBinding(action) switch
        {
            InputEventKey key => KeyName(key),
            InputEventJoypadButton button => ButtonName(button.ButtonIndex),
            InputEventJoypadMotion motion => AxisName(motion.Axis),
            InputEventMouseButton mouse => MouseName(mouse.ButtonIndex),
            InputEvent other => other.AsText(),
            null => action
        };
    }

    public static ButtonGlyph.Shape GetGlyph(string action)
    {
        if (GetBinding(action) is not InputEventJoypadButton button)
        {
            return ButtonGlyph.Shape.None;
        }

        bool playStation = Family == ControllerFamily.PlayStation;

        return button.ButtonIndex switch
        {
            JoyButton.DpadUp => ButtonGlyph.Shape.Up,
            JoyButton.DpadDown => ButtonGlyph.Shape.Down,
            JoyButton.DpadLeft => ButtonGlyph.Shape.Left,
            JoyButton.DpadRight => ButtonGlyph.Shape.Right,
            JoyButton.A when playStation => ButtonGlyph.Shape.Cross,
            JoyButton.B when playStation => ButtonGlyph.Shape.Circle,
            JoyButton.X when playStation => ButtonGlyph.Shape.Square,
            JoyButton.Y when playStation => ButtonGlyph.Shape.Triangle,
            _ => ButtonGlyph.Shape.None
        };
    }

    private static InputEvent GetBinding(string action)
    {
        bool controller = InputDevice.UsingController;
        return InputMap.ActionGetEvents(action)
            .FirstOrDefault(binding => (binding is InputEventJoypadButton or InputEventJoypadMotion) == controller);
    }

    private static string KeyName(InputEventKey keyEvent)
    {
        Key key = keyEvent.PhysicalKeycode != Key.None
            ? DisplayServer.KeyboardGetKeycodeFromPhysical(keyEvent.PhysicalKeycode)
            : keyEvent.Keycode;

        return OS.GetKeycodeString(key);
    }

    private static string MouseName(MouseButton button) => button switch
    {
        MouseButton.Left => "Left Click",
        MouseButton.Right => "Right Click",
        MouseButton.Middle => "Middle Click",
        MouseButton.WheelUp => "Wheel Up",
        MouseButton.WheelDown => "Wheel Down",
        _ => $"Mouse {(int)button}"
    };

    // Switch controllers read as Xbox for now.
    private static ControllerFamily Family
    {
        get
        {
            string name = Input.GetJoyName(InputDevice.LastJoypad).ToLowerInvariant();
            bool playStation = name.Contains("playstation") || name.Contains("dualshock") || name.Contains("dualsense")
                || name.Contains("ps3") || name.Contains("ps4") || name.Contains("ps5");

            return playStation ? ControllerFamily.PlayStation : ControllerFamily.Xbox;
        }
    }

    private static string ButtonName(JoyButton button)
    {
        bool ps = Family == ControllerFamily.PlayStation;

        return button switch
        {
            JoyButton.A => ps ? "Cross" : "A",
            JoyButton.B => ps ? "Circle" : "B",
            JoyButton.X => ps ? "Square" : "X",
            JoyButton.Y => ps ? "Triangle" : "Y",
            JoyButton.LeftShoulder => ps ? "L1" : "LB",
            JoyButton.RightShoulder => ps ? "R1" : "RB",
            JoyButton.LeftStick => ps ? "L3" : "LS",
            JoyButton.RightStick => ps ? "R3" : "RS",
            JoyButton.Back => ps ? "Share" : "View",
            JoyButton.Start => ps ? "Options" : "Menu",
            JoyButton.Touchpad => "Touchpad",
            JoyButton.DpadUp => "D-pad Up",
            JoyButton.DpadDown => "D-pad Down",
            JoyButton.DpadLeft => "D-pad Left",
            JoyButton.DpadRight => "D-pad Right",
            _ => $"Button {(int)button}"
        };
    }

    private static string AxisName(JoyAxis axis)
    {
        bool ps = Family == ControllerFamily.PlayStation;

        return axis switch
        {
            JoyAxis.TriggerLeft => ps ? "L2" : "LT",
            JoyAxis.TriggerRight => ps ? "R2" : "RT",
            JoyAxis.LeftX or JoyAxis.LeftY => "Left Stick",
            JoyAxis.RightX or JoyAxis.RightY => "Right Stick",
            _ => $"Axis {(int)axis}"
        };
    }
}
