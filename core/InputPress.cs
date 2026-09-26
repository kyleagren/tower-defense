using Godot;

// A press or release that just happened, counted once per real press. A trigger streams a fresh
// event for every small movement past its deadzone, so for those this asks Input which event
// actually started the press. That by-event check can't be used for mouse buttons: the viewport
// hands handlers a transformed copy of every mouse event, never the instance Input recorded - but
// a button only ever sends one press anyway.
public static class InputPress
{
    public static bool Began(InputEvent @event, string action) =>
        @event is InputEventJoypadMotion
            ? Input.IsActionJustPressedByEvent(action, @event)
            : @event.IsActionPressed(action);

    public static bool Ended(InputEvent @event, string action) =>
        @event is InputEventJoypadMotion
            ? Input.IsActionJustReleasedByEvent(action, @event)
            : @event.IsActionReleased(action);
}
