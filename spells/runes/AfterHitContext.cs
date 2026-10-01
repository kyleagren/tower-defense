using System.Collections.Generic;

// What an after-hit rune sees: the hit as it finally landed.
public readonly struct AfterHitContext
{
    public Enemy Target { get; init; }
    public DamagePacket Damage { get; init; }
    public IReadOnlyList<Tag> SpellTags { get; init; }
    public Attribution Attribution { get; init; }
    // The Status the hit found on arrival - Shatter checks this, since the hit itself may have
    // replaced or spent it.
    public Tag? StatusBefore { get; init; }
    public bool Killed { get; init; }
}
