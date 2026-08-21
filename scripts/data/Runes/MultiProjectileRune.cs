using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class MultiProjectileRune : Rune
{
    [Export] public int ExtraProjectiles { get; private set; }

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Projectile };

    public override void Apply(SpellCastContext context)
    {
        if (context is ProjectileCastContext projectileContext)
        {
            projectileContext.ProjectileCount += ExtraProjectiles;
        }
    }
}