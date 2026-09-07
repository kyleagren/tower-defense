using System.Collections.Generic;

// Resolves and caches a CharacterSheet per CharacterId from that character's equipped loadout.
// Invalidation is driven by EventBus.InventoryChanged, wired once in SaveManager._Ready - every
// path that changes a loadout already fires that signal, so nothing has to remember to invalidate
// by hand. (It used to hang off InventoryPanel.Refresh, which meant a world pickup that
// auto-equipped while the panel was closed left every consumer reading stale bonuses.)
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

    // A loadout change can only affect the character who owns it, but the signal doesn't say who
    // that was - and resolving a sheet is a handful of dictionary adds, so clearing all of them is
    // cheaper than threading an identity through every emitter.
    public static void InvalidateAll()
    {
        _cache.Clear();
    }

    private static CharacterSheet Resolve(CharacterId character)
    {
        Dictionary<StatId, float> bonuses = new();
        Dictionary<StatId, float> localTotals = new();

        if (!SaveManager.Instance.Data.Loadouts.TryGetValue(character, out EquippedLoadout loadout))
        {
            return new CharacterSheet(bonuses, localTotals);
        }

        foreach (Item item in loadout.EquippedItems())
        {
            foreach (RolledAffix roll in item.Affixes)
            {
                // Armor/Shield (and the Vanguard hybrid roll) resolve locally per-item
                // (Armor.ResolvedValue) instead of stacking into the shared global pool every
                // other stat uses.
                if (roll.Stat == StatId.SentinelArmor || roll.Stat == StatId.SentinelShield
                    || roll.Stat == StatId.SentinelArmorAndShield)
                {
                    continue;
                }

                bonuses.TryGetValue(roll.Stat, out float current);
                bonuses[roll.Stat] = current + roll.Value;
            }

            if (item is Armor armor)
            {
                localTotals.TryGetValue(StatId.SentinelArmor, out float armorTotal);
                localTotals[StatId.SentinelArmor] = armorTotal + armor.ResolvedValue(StatId.SentinelArmor);

                localTotals.TryGetValue(StatId.SentinelShield, out float shieldTotal);
                localTotals[StatId.SentinelShield] = shieldTotal + armor.ResolvedValue(StatId.SentinelShield);
            }
        }

        return new CharacterSheet(bonuses, localTotals);
    }
}
