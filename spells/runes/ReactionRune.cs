// A rune that spends an enemy's existing Status. Carries nothing of its own yet: it's a named
// category because a Ritual may socket only one, and because the per-enemy internal cooldown a
// disruptive reaction needs belongs here rather than on every OnHitRune.
public abstract partial class ReactionRune : OnHitRune
{
}
