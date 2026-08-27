using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ProjectileSpeedRune : Rune
{
    [Export] public float IncreasedSpeedPercent { get; private set; } = 0.3f;

    public override string Name => "Velocity";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Projectile };

    public override string Description => "Increases projectile speed by 30%.";

    public override void Apply(SpellCastContext context)
    {
        if (context.Projectile != null)
        {
            context.Projectile.IncreasedSpeedPercent += IncreasedSpeedPercent;
        }
    }
}
