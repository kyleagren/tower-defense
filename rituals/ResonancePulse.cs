using Godot;

// One per cast, carried on every hit the cast deals: the first of them to land pulses the Resonant
// Rituals around where the cast came from. The cast holds it rather than the Ritual, so a shot still
// pulses after its Ritual breaks or is torn down mid-flight, and two casts in the air at once each
// get their own pulse.
public class ResonancePulse
{
    public Vector3 Origin { get; init; }
    // So a cast never pulses the Ritual that made it.
    public Ritual Caster { get; init; }
    public bool Sent { get; set; }
}
