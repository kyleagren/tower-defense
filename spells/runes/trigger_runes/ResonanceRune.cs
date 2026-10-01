using Godot;

// Never casts on its own: casts whenever another Ritual - any player's - casts within its resonance
// radius, aimed at that Ritual's target. Its own cooldown still applies, so a pulse during it is
// dropped, and each Ritual fires at most once per cooldown however a cluster cascades.
public abstract partial class ResonanceRune : TriggerRune
{
    [Export] public float CooldownAdded { get; private set; }
    // Increased range for the spell only - its resonance radius doesn't grow with it.
    [Export] public float SpellRangeBonus { get; private set; }

    public override void Apply(SpellCastContext context)
    {
        context.Range.Increased += SpellRangeBonus;
        context.Trigger = new TriggerCastData { Resonant = true, CooldownAdded = CooldownAdded, RangeNotResonating = SpellRangeBonus };
    }
}
