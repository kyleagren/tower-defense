using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class FireballSpell : Spell
{
    [Export]
    public float Damage { get; set; }

    [Export]
    public int BaseProjectileCount { get; set; } = 1;
    public override IReadOnlyList<Tag> Tags => new List<Tag> { Tag.Projectile, Tag.Fire };

    protected override SpellCastContext CreateBaseContext()
    {
        return new ProjectileCastContext
        {
            Damage = Damage,
            ProjectileCount = BaseProjectileCount
        };
    }

}
