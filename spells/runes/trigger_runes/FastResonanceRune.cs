using Godot;

[GlobalClass]
public partial class FastResonanceRune : ResonanceRune
{
    [Export] public override float DamageMultiplier { get; protected set; } = 1.08f;

    public override string Name => "Fast Resonance";

    public override string Description =>
        $"This Ritual casts only when another Ritual casts within its range, at that Ritual's target - with no added cooldown. Deals {(DamageMultiplier - 1f) * 100f:0.#}% more damage.";
}
