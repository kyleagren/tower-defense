using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class FireballSpell : ChainableSpell
{
    [Export] public int BaseProjectileCount { get; private set; } = 1;
    [Export] public float BaseRadius { get; private set; } = 0.3f;
    [Export] public PackedScene ProjectileScene { get; private set; }

    public FireballSpell()
    {
        BaseDamage = 100f;
        BaseCastTime = 1.5f;
        BaseKnockbackForce = 5f;
    }

    public override string Name => "Fireball";

    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Projectile, Tag.Fire };

    public override string Description => "Hurls a fiery projectile that explodes on impact.";

    public override SpellCastContext CreateBaseContext()
    {
        return new ProjectileCastContext
        {
            Range = BaseRange,
            CastTime = BaseCastTime,
            Cooldown = BaseCooldown,
            Damage = BaseDamage,
            KnockbackForce = BaseKnockbackForce,
            ProjectileCount = BaseProjectileCount,
            Radius = BaseRadius,
            ProjectileScene = ProjectileScene,
            ChainCount = BaseChainCount,
            ChainRange = BaseChainRange
        };
    }
}
