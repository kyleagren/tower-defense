// Affixes in the same group can't sit on the same item together, on top of the rule that no stat
// rolls twice. None means only that per-stat rule applies.
public enum AffixGroup
{
    None,

    // Every element-specific Ritual damage mod - an item gets one element at most. Generic Ritual
    // damage is deliberately outside it, so it can sit alongside one element.
    ElementalRitualDamage
}
