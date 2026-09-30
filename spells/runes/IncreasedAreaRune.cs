using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class IncreasedAreaRune : Rune
{
    [Export] public float IncreasedAreaPercent { get; private set; } = 0.3f;

    public override string Name => "Expand";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.AoE };

    private static readonly Tag[] _tags = { Tag.AoE };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description => "Increases the area of effect by 30%.";

    public override void Apply(SpellCastContext context)
    {
        context.Area ??= new AreaCastData();
        context.Area.Multiplier.Increased += IncreasedAreaPercent;
    }
}
