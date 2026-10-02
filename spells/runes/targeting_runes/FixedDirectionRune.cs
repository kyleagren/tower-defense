using Godot;
using System;
using System.Collections.Generic;

// Fires in one direction picked at placement, at anything in the lane along it, instead of at a
// chosen target. Takes the targeting slot, since it replaces targeting outright.
[GlobalClass]
public partial class FixedDirectionRune : Rune
{
    private static readonly Tag[] _tags = { Tag.Targeting };
    private static readonly Tag[] _requiredAnyTags = { Tag.Projectile, Tag.Line, Tag.Traveling };

    [Export] public override float DamageMultiplier { get; protected set; } = 1.1f;
    // How far either side of the line an enemy can be and still set it off.
    [Export] public float LaneHalfWidth { get; private set; } = 1.5f;

    public override string Name => "Bearing";
    public override Type ExclusiveGroup => typeof(TargetingRune);
    public override IReadOnlyList<Tag> Tags => _tags;
    public override IReadOnlyList<Tag> RequiredTags => Array.Empty<Tag>();
    public override IReadOnlyList<Tag> RequiredAnyTags => _requiredAnyTags;
    public override float ManaCostMultiplier { get; protected set; } = 1.1f;

    public override string Description =>
        $"This ritual fires in a fixed direction, chosen when it's placed, at anything in its path. Deals {(DamageMultiplier - 1f) * 100f:0.#}% more damage.";

    public override void Apply(SpellCastContext context)
    {
        context.FixedAim ??= new FixedAimCastData();
        context.FixedAim.LaneHalfWidth = LaneHalfWidth;
    }
}
