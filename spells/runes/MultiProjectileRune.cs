using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class MultiProjectileRune : Rune
{
    [Export] public int ExtraProjectiles { get; private set; } = 2;
    [Export] public override float DamageMultiplier { get; protected set; } = 0.75f;

    public override string Name => "Salvo";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Projectile };

    private static readonly Tag[] _tags = { Tag.Trajectory };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description =>
        "Fires additional projectiles in a forward cone, trading a small amount of damage for extra hits.";

    public override void Apply(SpellCastContext context)
    {
        if (context.Projectile != null)
        {
            context.Projectile.ProjectileCount += ExtraProjectiles;
        }
    }
}
