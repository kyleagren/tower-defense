using System.Collections.Generic;

// Aggregated equipment (and later talent) bonuses for one CharacterId, keyed by StatId. Holds
// bonuses only - base values stay on whatever field they've always lived on (PlayerCharacter.Speed,
// a spell's own BaseX, etc.) - so each consumer combines its own base with GetBonus(...) itself,
// deciding whether that stat is additive or multiplicative for its case.
public class CharacterSheet
{
    private readonly Dictionary<StatId, float> _bonuses;
    private readonly Dictionary<StatId, float> _localTotals;

    public CharacterSheet(Dictionary<StatId, float> bonuses, Dictionary<StatId, float> localTotals)
    {
        _bonuses = bonuses;
        _localTotals = localTotals;
    }

    public float GetBonus(StatId stat)
    {
        return _bonuses.TryGetValue(stat, out float value) ? value : 0f;
    }

    // Already-resolved per-item totals (each item's own base * its own local % rolls, summed) -
    // for stats with no other character-level base at all (Armor/Shield - a naked Sentinel has
    // none). A further global multiplier (e.g. SentinelTotalArmor from jewelry) is a separate
    // GetBonus lookup a consumer applies on top, same as any other stat.
    public float GetLocalTotal(StatId stat)
    {
        return _localTotals.TryGetValue(stat, out float value) ? value : 0f;
    }
}
