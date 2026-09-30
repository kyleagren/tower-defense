using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class CastSpeedRune : Rune
{
    [Export] public float IncreasedCastSpeedPercent { get; private set; } = 0.2f;

    public override string Name => "Haste";

    public override IReadOnlyList<Tag> RequiredTags => Array.Empty<Tag>();

    private static readonly Tag[] _tags = { Tag.Cooldown };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description => "Reduces cast time, letting this ritual act more often.";

    public override void Apply(SpellCastContext context)
    {
        context.CastSpeed.Increased += IncreasedCastSpeedPercent;
    }
}
