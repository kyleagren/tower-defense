using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ShroudRune : Rune
{
    public override string Name => "Shroud";

    public override IReadOnlyList<Tag> RequiredTags => new List<Tag>();
    public override string Description => "Enemies can't see this ritual, and walk straight through it.";

    public override void Apply(SpellCastContext context)
    {
        context.Shrouded = true;
    }
}
