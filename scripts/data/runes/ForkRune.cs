using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ForkRune : Rune
{
    [Export] public int ExtraForks { get; private set; } = 1;
    [Export] public float DamageMultiplier { get; private set; } = 0.6f;

    public override string Name => "Fork";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Chaining };

    public override string Description => "Strikes an additional nearby enemy at the same time, each starting its own chain.";

    public override void Apply(SpellCastContext context)
    {
        context.Chain ??= new ChainCastData();
        context.Chain.ForkCount += ExtraForks;
        context.DamageScale.More *= DamageMultiplier;
    }
}
