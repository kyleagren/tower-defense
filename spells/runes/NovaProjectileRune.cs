using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class NovaProjectileRune : Rune
{
    [Export] public int ExtraProjectiles { get; private set; } = 7;
    [Export] public override float DamageMultiplier { get; protected set; } = 0.4f;

    public override string Name => "Nova";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Projectile };

    private static readonly Tag[] _tags = { Tag.Trajectory };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description =>
        "Splits the spell into a ring of projectiles fired outward in all directions along the horizontal, trading damage for coverage.";

    public override void Apply(SpellCastContext context)
    {
        if (context.Projectile != null)
        {
            context.Projectile.ProjectileCount += ExtraProjectiles;
            context.Projectile.IsNova = true;
        }
    }
}
