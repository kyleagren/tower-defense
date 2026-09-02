using System.Collections.Generic;

// Resolves and caches a CharacterSheet per CharacterId from that character's equipped loadout.
// Invalidate whenever a loadout changes (equip/unequip today, talent changes later) - see
// InventoryPanel.Refresh.
public static class CharacterSheetCache
{
    private static readonly Dictionary<CharacterId, CharacterSheet> _cache = new();

    public static CharacterSheet GetSheet(CharacterId character)
    {
        if (!_cache.TryGetValue(character, out CharacterSheet sheet))
        {
            sheet = Resolve(character);
            _cache[character] = sheet;
        }

        return sheet;
    }

    public static void Invalidate(CharacterId character)
    {
        _cache.Remove(character);
    }

    private static CharacterSheet Resolve(CharacterId character)
    {
        Dictionary<StatId, float> bonuses = new();

        if (!SaveManager.Instance.Data.Loadouts.TryGetValue(character, out EquippedLoadout loadout))
        {
            return new CharacterSheet(bonuses);
        }

        foreach (Item item in loadout.EquippedItems())
        {
            foreach (RolledAffix roll in item.Affixes)
            {
                bonuses.TryGetValue(roll.Stat, out float current);
                bonuses[roll.Stat] = current + roll.Value;
            }
        }

        return new CharacterSheet(bonuses);
    }
}
