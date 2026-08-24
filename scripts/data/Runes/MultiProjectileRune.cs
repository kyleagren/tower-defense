using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class MultiProjectileRune : Rune
{
    [Export] public int ExtraProjectiles { get; private set; }
    [Export] public float DamageMultiplier { get; private set; } = 0.9f;

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Projectile };

    public override string Description =>
        "Fires additional projectiles in a forward cone, trading a small amount of damage for extra hits.";

    public override void Apply(SpellCastContext context)
    {
        if (context is ProjectileCastContext projectileContext)
        {
            projectileContext.ProjectileCount += ExtraProjectiles;
            projectileContext.MoreDamageMultiplier *= DamageMultiplier;
        }
    }
}