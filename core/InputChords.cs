using Godot;

// Controller chords: holding L1 turns the face buttons into abilities, holding R1 turns the d-pad
// into tokens. A keyboard has its own key for every action and never holds a modifier, so its
// presses always pass straight through.
public static class InputChords
{
    public static bool AbilityModifierHeld => Input.IsActionPressed("ability_modifier");
    public static bool TokenModifierHeld => Input.IsActionPressed("token_modifier");

    public static bool IsAbilityPressed(InputEvent @event, string action) =>
        @event.IsActionPressed(action) && (@event is not InputEventJoypadButton || AbilityModifierHeld);

    // For the face-button actions that give way to abilities while L1 is held.
    public static bool IsPressedWithoutAbilityModifier(InputEvent @event, string action) =>
        @event.IsActionPressed(action) && !AbilityModifierHeld;
}
