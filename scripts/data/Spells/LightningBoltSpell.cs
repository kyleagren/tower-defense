using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class LightningBoltSpell : ChainableSpell
{
    public LightningBoltSpell()
    {
        BaseDamage = 40f;
        BaseCastTime = 0.6f;
        BaseChainCount = 2;
    }

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
