using Godot;
using System.Collections.Generic;

// The spell-side half of a hit: which on-hit runes to run, the spell tags they read, and whose spell
// it was. These always travel together from a cast to wherever its damage lands, so they move as one
// value rather than as separate parameters through every damage site.
public readonly struct SpellHit
{
    public IReadOnlyList<OnHitRune> OnHitRunes { get; init; }
    public IReadOnlyList<Tag> SpellTags { get; init; }
    public Attribution Attribution { get; init; }
    public ResonancePulse ResonancePulse { get; init; }

    // Hits on ChosenTarget may land harder, if TargetingRune says so.
    public Enemy ChosenTarget { get; init; }
    public TargetingRune TargetingRune { get; init; }

    // How every spell damage site builds its hit, so none of them can drop the runes or the credit.
    public Hit CreateHit(DamagePacket damage, Vector3 ragdollImpulse) =>
        new Hit { Damage = damage, RagdollImpulse = ragdollImpulse, Attribution = Attribution, Spell = this };
}
