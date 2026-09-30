using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class PierceRune : Rune
{
    [Export] public int ExtraPierce { get; private set; } = 2;

    public override string Name => "Pierce";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag> { Tag.Projectile };

    private static readonly Tag[] _tags = { Tag.Trajectory };

    public override IReadOnlyList<Tag> Tags => _tags;

    public override string Description => "Projectiles pass through additional enemies before stopping.";

    public override void Apply(SpellCastContext context)
    {
        if (context.Projectile != null)
        {
            context.Projectile.PierceCount += ExtraPierce;
        }
    }
}
