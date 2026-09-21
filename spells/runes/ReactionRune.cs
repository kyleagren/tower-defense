using System;

// A rune that spends an enemy's existing Status. A Ritual fields only one, since two would both
// cash in the same Status on the same hit - and the per-enemy internal cooldown a disruptive
// reaction needs belongs here rather than on every OnHitRune.
public abstract partial class ReactionRune : OnHitRune
{
    public override Type ExclusiveGroup => typeof(ReactionRune);
}
