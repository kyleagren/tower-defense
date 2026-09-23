using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class LightningBoltSpell : ChainableSpell
{
    [Export] public override float BaseLightningDamage { get; protected set; } = 20f;
    [Export] public override float BaseCastTime { get; protected set; } = 0.6f;
    [Export] public override int BaseChainCount { get; protected set; } = 2;
    [Export] public override int BaseManaCost { get; protected set; } = 10;
    [Export] public override ChainDamageModifier DamageModifier { get; protected set; }
    [Export] public PackedScene ArcVisualScene { get; private set; }

    public override string Name => "Lightning Bolt";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Chaining, Tag.Lightning };
    public override string Description => "Instantly strikes the nearest enemy, arcing to nearby foes.";

    public override SpellCastContext CreateBaseContext()
    {
        return new SpellCastContext
        {
            Range = BaseRange,
            CastTime = BaseCastTime,
            Cooldown = BaseCooldown,
            UnscaledDamage = BaseDamage,
            RagdollForce = BaseRagdollForce,
            ManaCost = BaseManaCost,
            CapacityCost = BaseCapacityCost,
            TargetPriority = BaseTargetPriority,
            CastEffect = new InstantHitCastEffect(),
            InstantHit = new InstantHitCastData { ArcVisualScene = ArcVisualScene },
            Chain = new ChainCastData
            {
                ChainCount = BaseChainCount,
                ChainRange = BaseChainRange,
                DamageModifier = DamageModifier
            }
        };
    }
}
