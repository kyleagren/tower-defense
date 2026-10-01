using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class RangeRune : Rune
{
    [Export] public float IncreasedRangePercent { get; private set; } = 0.25f;

    public override string Name => "Extend";

    public override IReadOnlyList<Tag> RequiredTags => Array.Empty<Tag>();

    private static readonly Tag[] _tags = { Tag.Utility };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description => $"Increases this ritual's range by {Mathf.RoundToInt(IncreasedRangePercent * 100f)}%.";

    public override void Apply(SpellCastContext context)
    {
        context.Range.Increased += IncreasedRangePercent;
    }
}
