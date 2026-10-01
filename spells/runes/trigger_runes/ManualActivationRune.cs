using Godot;

[GlobalClass]
public partial class ManualActivationRune : TriggerRune
{
    [Export] public override float DamageMultiplier { get; protected set; } = 2f;
    [Export] public float CooldownMultiplier { get; private set; } = 2f;
    [Export] public float CooldownAdded { get; private set; } = 5f;

    public override string Name => "Manual Activation";

    public override string Description =>
        $"This Ritual never casts on its own - any player fires it with {InputPromptHelper.GetPrompt("interact")}. Deals {(DamageMultiplier - 1f) * 100f:0.#}% more damage; its cooldown becomes double, or {CooldownAdded:0.#}s longer, whichever is more.";

    public override void Apply(SpellCastContext context)
    {
        context.Trigger = new TriggerCastData { Manual = true, CooldownMultiplier = CooldownMultiplier, CooldownAdded = CooldownAdded };
    }
}
