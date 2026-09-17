using System.Collections.Generic;

// The spell-side half of an on-hit callback, handed to Enemy.TakeDamage alongside the damage: which
// runes to run, and the spell tags they read. The two always travel together, so they move as one
// value rather than as a second parameter through every damage site.
public readonly struct SpellHit
{
    public IReadOnlyList<OnHitRune> OnHitRunes { get; init; }
    public IReadOnlyList<Tag> SpellTags { get; init; }
}
