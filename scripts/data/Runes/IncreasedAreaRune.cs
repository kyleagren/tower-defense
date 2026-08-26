using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class IncreasedAreaRune : Rune
{
    [Export] public float AreaMultiplier { get; private set; } = 1.3f;

    public override string Name => "Increased Area";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.AoE };

    public override string Description => "Increases the area of effect by 30%.";

    public override void Apply(SpellCastContext context)
    {
        context.AreaMultiplier *= AreaMultiplier;
    }
}
