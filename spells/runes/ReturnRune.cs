using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ReturnRune : Rune
{
    [Export] public override float DamageMultiplier { get; protected set; } = 0.6f;

    public override string Name => "Return";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Projectile };

    private static readonly Tag[] _tags = { Tag.Trajectory };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description => "Projectiles fly back toward their origin instead of expiring.";

    public override void Apply(SpellCastContext context)
    {
        if (context.Projectile != null)
        {
            context.Projectile.IsReturn = true;
        }
    }
}
