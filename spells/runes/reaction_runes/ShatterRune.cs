using Godot;
using System.Collections.Generic;

// Cold and Lightning meeting, either way round. Spends the Status on every attempt - that's the cost
// of trying - and executes an enemy the hit leaves below the threshold. An execute isn't crowd
// control, so it needs no cooldown.
[GlobalClass]
public partial class ShatterRune : ReactionRune
{
    [Export] public float ExecuteThreshold { get; private set; } = 0.1f;

    public override string Name => "Shatter";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag>();
    public override IReadOnlyList<Tag> RequiredAnyTags => new List<Tag> { Tag.Cold, Tag.Lightning };

    public override string Description =>
        $"When Cold and Lightning meet on an enemy, consumes its Status and shatters it outright if left below {ExecuteThreshold * 100f:0.#}% health.";

    public override string EffectLabel => "Shatters";

    public override bool OnHit(OnHitContext hit) => IsPair(hit.Target.Status, hit.Damage.DominantElement);

    // After the hit, so the hit that takes an enemy under the threshold is the one that executes it.
    public override void AfterHit(AfterHitContext hit)
    {
        if (hit.Killed || !IsPair(hit.StatusBefore, hit.Damage.DominantElement) || hit.Target.HealthFraction > ExecuteThreshold)
        {
            return;
        }

        CountEffect(hit.Attribution);
        hit.Target.Execute(hit.Attribution, hit.Damage.DominantElement.Value);
    }

    private static bool IsPair(Tag? status, Tag? incoming) =>
        (status == Tag.Cold && incoming == Tag.Lightning) || (status == Tag.Lightning && incoming == Tag.Cold);
}
