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
