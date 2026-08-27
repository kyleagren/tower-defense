using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class FireballSpell : Spell
{
    [Export] public PackedScene ProjectileScene { get; private set; }
    [Export] public int BaseProjectileCount { get; private set; } = 1;
    [Export] public float BaseRadius { get; private set; } = 0.2f;
    [Export] public ProjectileHitEffect HitEffect { get; private set; }

    [Export] public override float BaseFireDamage { get; protected set; } = 100f;
    [Export] public override float BaseCastTime { get; protected set; } = 1.5f;
    [Export] public override float BaseRagdollForce { get; protected set; } = 20f;
    [Export] public override int BudgetCost { get; protected set; } = 10;

    public override string Name => "Fireball";
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Projectile, Tag.Fire, Tag.AoE };
    public override string Description => "Hurls a fiery projectile that explodes on impact.";

    public override SpellCastContext CreateBaseContext()
    {
        return new SpellCastContext
        {
            Range = BaseRange,
            CastTime = BaseCastTime,
            Cooldown = BaseCooldown,
            Damage = BaseDamage,
            RagdollForce = BaseRagdollForce,
            Projectile = new ProjectileCastData
            {
                ProjectileCount = BaseProjectileCount,
                Radius = BaseRadius,
                ProjectileScene = ProjectileScene,
                HitEffect = HitEffect
            },
            Area = new AreaCastData()
        };
    }
}
