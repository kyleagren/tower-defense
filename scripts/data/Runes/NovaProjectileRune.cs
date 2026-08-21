using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class NovaProjectileRune : Rune
{
    [Export] public int ExtraProjectiles { get; private set; } = 7;
    [Export] public float DamageMultiplier { get; private set; } = 0.7f;

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Projectile };

    public override void Apply(SpellCastContext context)
    {
        if (context is ProjectileCastContext projectileContext)
        {
            projectileContext.ProjectileCount += ExtraProjectiles;
            projectileContext.IsNova = true;
            projectileContext.Damage *= DamageMultiplier;
        }
    }
}