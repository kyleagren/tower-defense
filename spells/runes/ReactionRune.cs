using System;
using System.Collections.Generic;

// A rune that spends an enemy's existing Status. A Ritual fields only one, since two would both
// cash in the same Status on the same hit.
public abstract partial class ReactionRune : OnHitRune
{
    private static readonly Tag[] _tags = { Tag.Reaction };

    public override Type ExclusiveGroup => typeof(ReactionRune);

    public override IReadOnlyList<Tag> Tags => _tags;
}
