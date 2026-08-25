using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ChainRune : Rune
{
    [Export] public int ExtraChains { get; private set; } = 3;
    [Export] public float DamageMultiplier { get; private set; } = 0.8f;

    public override string Name => "Chain";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag>();
    public override IReadOnlyList<Tag> RequiredAnyTags => new List<Tag> { Tag.Chaining, Tag.Projectile };

    public override string Description => "Adds additional chain jumps to nearby enemies.";

    public override void Apply(SpellCastContext context)
    {
        if (context is ChainableSpellCastContext chainable)
        {
            chainable.ChainCount += ExtraChains;
            chainable.MoreDamageMultiplier *= DamageMultiplier;
        }
    }
}
