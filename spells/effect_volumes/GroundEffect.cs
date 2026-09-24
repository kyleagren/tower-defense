using Godot;

// Persistent terrain at a location. Knows nothing about what spawned it, so a cast effect, a rune
// firing off a destruction signal, or a reaction can each drop one in a line.
public abstract partial class GroundEffect : EffectVolume
{
    protected override bool AffectsOnEntry => true;

    // Outlives one tick, so an enemy standing inside stays affected between reapplications and what
    // was applied lapses shortly after it leaves - which is why nothing tracks departures.
    protected float EffectDuration => TickInterval * 2f;

    // The terrain's strength, from whatever spawned it - ChilledGround reads it as its slow fraction.
    protected float Magnitude { get; private set; }

    public void Initialize(Vector3 position, float radius, float duration, float magnitude)
    {
        base.Initialize(position, radius, duration);
        Magnitude = magnitude;
    }
}
