using Godot;
using System.Collections.Generic;

// Fire consuming Cold, one way: a share of the enemy's max health, less against heavier enemies, at
// most once per enemy per Cooldown.
[GlobalClass]
public partial class MeltRune : ReactionRune
{
    [Export] public float MaxHealthPercent { get; private set; } = 0.12f;
    [Export] public float Cooldown { get; private set; } = 5f;
    // Enemies at or under ReferenceMass take the full share; heavier ones less, by
    // (ReferenceMass / mass) ^ MassExponent - 12% on an 80kg Grunt, about 8% on a 400kg Brute.
    [Export] public float ReferenceMass { get; private set; } = 80f;
    [Export] public float MassExponent { get; private set; } = 0.25f;

    public override string Name => "Melt";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Fire };

    public override string Description =>
        $"When Fire meets a Cold Status, consumes it to melt away {MaxHealthPercent * 100f:0.#}% of the enemy's max health, less against heavier enemies. Once per enemy every {Cooldown:0.#}s.";

    // Spends the Status even while the enemy is still Melted - a blocked reaction still costs its
    // setup, like every cooldown-limited reaction.
    public override bool OnHit(OnHitContext hit)
    {
        Enemy target = hit.Target;

        if (target.Status != Tag.Cold || hit.Damage.DominantElement != Tag.Fire)
        {
            return false;
        }

        if (!target.HasDebuff(DebuffType.Melted))
        {
            float massFactor = Mathf.Min(1f, Mathf.Pow(ReferenceMass / target.Mass, MassExponent));
            target.ApplyDebuff(new MeltedDebuff(target.MaxHealth * MaxHealthPercent * massFactor, Cooldown, hit.Attribution));
        }

        return true;
    }
}
