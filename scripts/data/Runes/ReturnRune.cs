using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ReturnRune : Rune
{
    public override string Name => "Return";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Projectile };

    public override string Description => "Projectiles fly back toward their origin instead of expiring.";

    public override void Apply(SpellCastContext context)
    {
        if (context is ProjectileCastContext projectileContext)
        {
            projectileContext.IsReturn = true;
        }
    }
}
