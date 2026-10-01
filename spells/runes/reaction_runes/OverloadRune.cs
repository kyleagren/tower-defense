using Godot;
using System.Collections.Generic;

// Lightning consuming Fire, one way - the alternative to Ignition for the same pairing. A burst that
// throws enemies back is too disruptive to fire every attack cycle, so it triggers at most once per
// enemy per Cooldown.
[GlobalClass]
public partial class OverloadRune : ReactionRune
{
    [Export] public float BurstDamagePercent { get; private set; } = 0.4f;
    [Export] public float Radius { get; private set; } = 3f;
    // kg·m/s, like any knockback - Repel's strength.
    [Export] public float KnockbackImpulse { get; private set; } = 1600f;
    [Export] public float Cooldown { get; private set; } = 5f;
    [Export] public PackedScene BurstScene { get; private set; }

    public override string Name => "Overload";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Lightning };

    public override string Description =>
        $"When Lightning meets a Fire Status, consumes it to overload: a burst dealing {BurstDamagePercent * 100f:0.#}% of the hit as Lightning to everything within {Radius:0.#}m, throwing them back. Once per enemy every {Cooldown:0.#}s.";

    // Spends the Status even while the enemy is still Overloaded, like Melt.
    public override bool OnHit(OnHitContext hit)
    {
        Enemy target = hit.Target;

        if (target.Status != Tag.Fire || hit.Damage.DominantElement != Tag.Lightning)
        {
            return false;
        }

        if (!target.HasDebuff(DebuffType.Overloaded))
        {
            target.ApplyDebuff(new OverloadedDebuff(hit.Damage.Total * BurstDamagePercent, Radius, KnockbackImpulse, Cooldown, BurstScene, hit.Attribution));
        }

        return true;
    }
}
