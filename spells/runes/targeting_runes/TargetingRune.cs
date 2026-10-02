using Godot;
using System;
using System.Collections.Generic;

// A rune that decides which enemy in range its Ritual casts at, and hits that enemy harder. A Ritual
// fields one, since each replaces the priority outright.
public abstract partial class TargetingRune : Rune
{
    private static readonly Tag[] _tags = { Tag.Targeting };

    [Export] public float MoreDamageToTarget { get; private set; } = 0.1f;

    public override Type ExclusiveGroup => typeof(TargetingRune);

    public override IReadOnlyList<Tag> Tags => _tags;

    public override IReadOnlyList<Tag> RequiredTags => Array.Empty<Tag>();

    public override float ManaCostMultiplier { get; protected set; } = 1.1f;

    protected abstract TargetPriority Priority { get; }

    // Which enemy it picks; the damage bonus is appended to every one alike.
    protected abstract string TargetingDescription { get; }

    public override string Description => $"{TargetingDescription} Deals {MoreDamageToTarget * 100f:0.#}% more damage to its target.";

    // A spell that casts without a target (Ice Wall) has nothing to aim.
    public override bool IsCompatibleWith(Spell spell) => spell.RequiresTarget && base.IsCompatibleWith(spell);

    // Whether a hit on this rune's chosen target gets the bonus, checked before the hit lands.
    public virtual bool BoostsHitOn(Enemy target) => true;

    public override void Apply(SpellCastContext context)
    {
        context.TargetPriority = Priority;
        context.TargetingRune = this;
    }
}
