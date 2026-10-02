// Set by a trigger rune: the Ritual never casts on its own, only when pulsed - by a player pressing
// interact on it (Manual activation), or by the first hit of a cast from another Ritual within its
// resonance radius (the Resonance runes).
public class TriggerCastData
{
    public bool Manual;
    public bool Resonant;
    // Increased range the spell gets that its resonance radius doesn't (Far Resonance's own bonus).
    public float RangeNotResonating;
    // The cooldown becomes the greater of itself x CooldownMultiplier or itself + CooldownAdded.
    public float CooldownMultiplier = 1f;
    public float CooldownAdded;
}
