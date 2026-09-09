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
    private static void ScanDirectory(string path)
    {
        DirAccess dir = DirAccess.Open(path);

        if (dir == null)
        {
            return;
        }

        foreach (string fileName in dir.GetFiles().Where(f => f.EndsWith(".tres")))
        {
            Rune rune = GD.Load<Rune>(path + fileName);

            if (rune == null)
            {
                continue;
            }

            if (!_byRarity.TryGetValue(rune.Rarity, out List<Rune> list))
            {
                list = new List<Rune>();
                _byRarity[rune.Rarity] = list;
            }

            list.Add(rune);
        }

        foreach (string subDirectory in dir.GetDirectories())
        {
            ScanDirectory($"{path}{subDirectory}/");
        }
    }
}
