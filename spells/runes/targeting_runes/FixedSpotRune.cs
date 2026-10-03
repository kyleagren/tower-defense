using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Lands on one spot picked at placement, whenever an enemy is inside its area, instead of on a chosen
// target. Takes the targeting slot, since it replaces targeting outright.
[GlobalClass]
public partial class FixedSpotRune : Rune
{
    private static readonly Tag[] _tags = { Tag.Targeting };
    private static readonly Tag[] _requiredTags = { Tag.AoE };

    // Area spells that travel or fire along a line have no single spot to land on.
    private static readonly Tag[] _excludedTags = { Tag.Projectile, Tag.Line, Tag.Traveling };

    [Export] public override float DamageMultiplier { get; protected set; } = 1.1f;

    public override string Name => "Anchor";
    public override Type ExclusiveGroup => typeof(TargetingRune);
    public override IReadOnlyList<Tag> Tags => _tags;
    public override IReadOnlyList<Tag> RequiredTags => _requiredTags;
    public override float ManaCostMultiplier { get; protected set; } = 1.1f;

    public override string Description =>
        $"This ritual always lands on one spot, chosen when it's placed, whenever an enemy is there. Deals {(DamageMultiplier - 1f) * 100f:0.#}% more damage.";

    public override bool IsCompatibleWith(Spell spell) =>
        base.IsCompatibleWith(spell) && !spell.Tags.Any(tag => _excludedTags.Contains(tag));

    public override void Apply(SpellCastContext context)
    {
        context.FixedSpot ??= new FixedSpotCastData();
    }
}
