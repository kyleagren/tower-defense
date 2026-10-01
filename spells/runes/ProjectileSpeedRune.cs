using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ProjectileSpeedRune : Rune
{
    [Export] public float IncreasedSpeedPercent { get; private set; } = 0.25f;

    public override string Name => "Velocity";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Projectile };

    private static readonly Tag[] _tags = { Tag.Trajectory };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description => $"Increases projectile speed by {Mathf.RoundToInt(IncreasedSpeedPercent * 100f)}%.";

    public override void Apply(SpellCastContext context)
    {
        if (context.Projectile != null)
        {
            context.Projectile.Speed.Increased += IncreasedSpeedPercent;
        }
    }
}
