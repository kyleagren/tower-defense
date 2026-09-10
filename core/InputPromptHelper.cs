using Godot;
using System.Linq;

public static class InputPromptHelper
{
    public static string GetActionKeyName(string action)
    {
        InputEvent first = InputMap.ActionGetEvents(action).FirstOrDefault();

        if (first is InputEventKey keyEvent)
        {
            Key key = keyEvent.PhysicalKeycode != Key.None
                ? DisplayServer.KeyboardGetKeycodeFromPhysical(keyEvent.PhysicalKeycode)
                : keyEvent.Keycode;

            return OS.GetKeycodeString(key);
        }

        return first?.AsText() ?? action;
    }
}
