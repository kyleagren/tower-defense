using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class LightningBoltSpell : ChainableSpell
{
    [Export] public override float BaseDamage { get; protected set; } = 40f;
    [Export] public override float BaseCastTime { get; protected set; } = 0.6f;
    [Export] public override int BaseChainCount { get; protected set; } = 2;
    [Export] public override int BudgetCost { get; protected set; } = 10;

    public override string Name => "Lightning Bolt";

    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Chaining, Tag.Lightning };

    public override string Description => "Instantly strikes the nearest enemy, arcing to nearby foes.";

    public override SpellCastContext CreateBaseContext()
    {
        return new InstantHitCastContext
        {
            Range = BaseRange,
            CastTime = BaseCastTime,
            Cooldown = BaseCooldown,
            Damage = BaseDamage,
            KnockbackForce = BaseKnockbackForce,
            ChainCount = BaseChainCount,
            ChainRange = BaseChainRange
        };
    }
}
