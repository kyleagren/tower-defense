using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class ProjectileSpeedRune : Rune
{
    [Export] public float IncreasedSpeedPercent { get; private set; } = 0.25f;

    public override string Name => "Velocity";

    public override IReadOnlyList<Tag> RequiredTags => Array.Empty<Tag>();
    public override IReadOnlyList<Tag> RequiredAnyTags => new List<Tag> { Tag.Projectile, Tag.Traveling };

    private static readonly Tag[] _tags = { Tag.Trajectory };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description => $"Projectiles and traveling spells move {Mathf.RoundToInt(IncreasedSpeedPercent * 100f)}% faster.";

    public override void Apply(SpellCastContext context)
    {
        if (context.Projectile != null)
        {
            context.Projectile.Speed.Increased += IncreasedSpeedPercent;
        }

        if (context.Sweep != null)
        {
            context.Sweep.Speed.Increased += IncreasedSpeedPercent;
        }
    }
}
