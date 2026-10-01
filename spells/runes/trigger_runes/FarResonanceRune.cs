using Godot;

[GlobalClass]
public partial class FarResonanceRune : ResonanceRune
{
    [Export] public override float DamageMultiplier { get; protected set; } = 1.25f;

    public override string Name => "Far Resonance";

    public override string Description =>
        $"This Ritual casts only when another Ritual casts within its range, at that Ritual's target, and reaches {SpellRangeBonus * 100f:0.#}% further than it listens. Deals {(DamageMultiplier - 1f) * 100f:0.#}% more damage; its cooldown is {CooldownAdded:0.#}s longer.";
}
