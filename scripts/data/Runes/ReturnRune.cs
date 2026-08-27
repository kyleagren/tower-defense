using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ReturnRune : Rune
{
    [Export] public float DamageMultiplier { get; private set; } = 0.6f;

    public override string Name => "Return";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Projectile };

    public override string Description => "Projectiles fly back toward their origin instead of expiring.";

    public override void Apply(SpellCastContext context)
    {
        if (context.Projectile != null)
        {
            context.Projectile.IsReturn = true;
            context.MoreDamageMultiplier *= DamageMultiplier;
        }
    }
}
