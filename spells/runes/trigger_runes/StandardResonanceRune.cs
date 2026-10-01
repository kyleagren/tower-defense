using Godot;

[GlobalClass]
public partial class StandardResonanceRune : ResonanceRune
{
    [Export] public override float DamageMultiplier { get; protected set; } = 1.3f;

    public override string Name => "Resonance";

    public override string Description =>
        $"This Ritual casts only when another Ritual casts within its range, at that Ritual's target. Deals {(DamageMultiplier - 1f) * 100f:0.#}% more damage; its cooldown is {CooldownAdded:0.#}s longer.";
}
