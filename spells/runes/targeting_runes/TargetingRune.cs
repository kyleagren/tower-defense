using System;
using System.Collections.Generic;

// A rune that decides which enemy in range its Ritual casts at. A Ritual fields one, since each
// replaces the priority outright.
public abstract partial class TargetingRune : Rune
{
    private static readonly Tag[] _tags = { Tag.Targeting };

    public override Type ExclusiveGroup => typeof(TargetingRune);

    public override IReadOnlyList<Tag> Tags => _tags;

    public override IReadOnlyList<Tag> RequiredTags => Array.Empty<Tag>();

    public override float ManaCostMultiplier { get; protected set; } = 1.1f;

    protected abstract TargetPriority Priority { get; }

    // A spell that casts without a target (Ice Wall) has nothing to aim.
    public override bool IsCompatibleWith(Spell spell) => spell.RequiresTarget && base.IsCompatibleWith(spell);

    public override void Apply(SpellCastContext context)
    {
        context.TargetPriority = Priority;
        context.TargetPriorityFromRune = true;
    }
}
