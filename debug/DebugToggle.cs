using Godot;

public partial class DebugToggle : Node
{
    private const float DebugHeatStep = 20f;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        // Nudges the Tank's heat dial either way, for testing it without a wave's worth of hits.
        if (keyEvent.PhysicalKeycode == Key.F1)
        {
            (Sentinel.Local as Tank)?.AddHeat(DebugHeatStep);
        }
        else if (keyEvent.PhysicalKeycode == Key.F2)
        {
            (Sentinel.Local as Tank)?.AddHeat(-DebugHeatStep);
        }
    }
}
