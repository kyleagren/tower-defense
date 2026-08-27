using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class RangeRune : Rune
{
    [Export] public float IncreasedRangePercent { get; private set; } = 0.3f;

    public override string Name => "Reach";

    public override IReadOnlyList<Tag> RequiredTags => Array.Empty<Tag>();

    public override string Description => "Increases this tower's range by 30%.";

    public override void Apply(SpellCastContext context)
    {
        context.IncreasedRangePercent += IncreasedRangePercent;
    }
}
