using System.Collections.Generic;

// What an OnHitRune is handed when a spell's damage lands. A value with named members rather than a
// growing parameter list: a rune that needs the spell's own tags - to tell a spell built around an
// element from one that merely happens to deal some of it - would otherwise mean re-threading every
// damage site to reach it.
public readonly struct OnHitContext
{
    public Enemy Target { get; init; }
    public DamagePacket Damage { get; init; }
    public IReadOnlyList<Tag> SpellTags { get; init; }

    // Who dealt the hit - passed on to anything the rune starts, like a Burn, so its damage is
    // credited to the same player.
    public Attribution Attribution { get; init; }
}
