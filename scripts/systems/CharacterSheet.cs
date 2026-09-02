using System.Collections.Generic;

// Aggregated equipment (and later talent) bonuses for one CharacterId, keyed by StatId. Holds
// bonuses only - base values stay on whatever field they've always lived on (PlayerCharacter.Speed,
// a spell's own BaseX, etc.) - so each consumer combines its own base with GetBonus(...) itself,
// deciding whether that stat is additive or multiplicative for its case.
public class CharacterSheet
{
    private readonly Dictionary<StatId, float> _bonuses;

    public CharacterSheet(Dictionary<StatId, float> bonuses)
    {
        _bonuses = bonuses;
    }

    public float GetBonus(StatId stat)
    {
        return _bonuses.TryGetValue(stat, out float value) ? value : 0f;
    }
}
