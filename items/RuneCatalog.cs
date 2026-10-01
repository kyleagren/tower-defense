using Godot;
using System.Collections.Generic;
using System.Linq;

// Every authored Rune, grouped by Rarity - the one place that knows the full roster, so
// SaveManager (starting content) and LootGenerator (rune-tome drops) don't each scan the
// directory their own way.
public static class RuneCatalog
{
    private const string RunesDirectory = "res://assets/runes/";

    private static Dictionary<Rarity, List<Rune>> _byRarity;

    public static IReadOnlyList<Rune> AllOfRarity(Rarity rarity)
    {
        EnsureLoaded();
        return _byRarity.GetValueOrDefault(rarity, new List<Rune>());
    }

    public static IEnumerable<Rune> GetRunesOfType(Rune rune)
    {
        EnsureLoaded();
        return _byRarity.Values.SelectMany(list => list).Where(r => r.GetType() == rune.GetType());
    }

    // A non-Unlimited rune's Quantity is the total ever owned, never decremented on socketing - what's
    // actually available right now is derived live from how many currently-placed Rituals hold it, so
    // a crash (or any other path that skips Ritual.Destroy) can never desync it the way a stored
    // "remaining count" that's manually incremented back on destroy could.
    public static bool IsAvailable(Rune rune)
    {
        RuneInventoryEntry entry = PlayerProfiles.Local.Data.KnownRunes.FirstOrDefault(e => e.Rune == rune);

        if (entry == null || entry.Unlimited)
        {
            return true;
        }

        int inUse = LevelContext.Current.Rituals.Active
            .Where(ritual => ritual.OwnerId == PlayerProfiles.LocalId)
            .SelectMany(ritual => ritual.RuneSlots)
            .Count(socketed => socketed == rune);
        return inUse < entry.Quantity;
    }

    private static void EnsureLoaded()
    {
        if (_byRarity != null)
        {
            return;
        }

        _byRarity = new Dictionary<Rarity, List<Rune>>();
        ScanDirectory(RunesDirectory);
    }

    // Recurses into subfolders - each rune type gets its own folder (e.g. assets/runes/chain/)
    // to hold its five rarity-tier .tres files without the whole directory becoming one flat pile.
    // ResourceLoader.ListDirectory rather than DirAccess: an export converts and renames resource
    // files, and DirAccess sees the renamed ones, so a .tres filter would find nothing.
    private static void ScanDirectory(string path)
    {
        foreach (string entry in ResourceLoader.ListDirectory(path))
        {
            if (entry.EndsWith("/"))
            {
                ScanDirectory(path + entry);
                continue;
            }

            if (!entry.EndsWith(".tres"))
            {
                continue;
            }

            Rune rune = GD.Load<Rune>(path + entry);

            if (rune == null)
            {
                continue;
            }

            RuneTags.Validate(rune);

            if (!_byRarity.TryGetValue(rune.Rarity, out List<Rune> list))
            {
                list = new List<Rune>();
                _byRarity[rune.Rarity] = list;
            }

            list.Add(rune);
        }
    }
}
