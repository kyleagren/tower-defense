using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class FireballSpell : Spell
{
    [Export] public float Damage { get; private set; } = 100;
    [Export] public int BaseProjectileCount { get; private set; } = 1;
    [Export] public float BaseRadius { get; private set; } = 0.3f;

    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Projectile, Tag.Fire };

    public override SpellCastContext CreateBaseContext()
    {
        return new ProjectileCastContext
        {
            Range = BaseRange,
            Cooldown = BaseCooldown,
            Damage = Damage,
            ProjectileCount = BaseProjectileCount,
            Radius = BaseRadius
        };
    }
}