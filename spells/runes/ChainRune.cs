using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ChainRune : Rune
{
    [Export] public int ExtraChains { get; private set; } = 3;
    [Export] public float FallbackChainRange { get; private set; } = 6f;
    [Export] public override float DamageMultiplier { get; protected set; } = 0.8f;

    public override string Name => "Chain";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag>();
    public override IReadOnlyList<Tag> RequiredAnyTags => new List<Tag> { Tag.Chaining, Tag.Projectile };

    private static readonly Tag[] _tags = { Tag.Trajectory };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description => "Adds additional chain jumps to nearby enemies.";

    public override void Apply(SpellCastContext context)
    {
        context.Chain ??= new ChainCastData();
        context.Chain.ChainCount += ExtraChains;
        context.Chain.ChainRange = Mathf.Max(context.Chain.ChainRange, FallbackChainRange);
    }
}
