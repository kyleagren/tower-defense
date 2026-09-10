using System.Collections.Generic;

// Random "flavor" title generator (PoE/Borderlands-style: "Doom Toe", "Time Chassis") - purely
// cosmetic, layered on top of Item.TypeLabel's mechanical description rather than replacing it.
// Every word list here is a deliberate placeholder - hand-author real ones later, this only needs
// the mechanism to work.
public static class NameGenerator
{
    private static readonly System.Random _random = new();

    // Shared across every item type.
    private static readonly string[] _firstWords = { "Doom", "Iron", "Storm", "Ember", "Shadow" };

    // One flavor noun-list per concrete item type - keeps combinations at least slot-appropriate.
    private static readonly Dictionary<System.Type, string[]> _secondWordsByType = new()
    {
        [typeof(Gun)] = new[] { "Barrel", "Cannon", "Repeater" },
        [typeof(SwordAndShield)] = new[] { "Edge", "Bulwark", "Guard" },
        [typeof(Helmet)] = new[] { "Crown", "Visor", "Hood" },
        [typeof(Chest)] = new[] { "Chassis", "Cuirass", "Husk" },
        [typeof(Legplates)] = new[] { "Greaves", "Stride", "March" },
        [typeof(Boots)] = new[] { "Toe", "Tread", "Step" },
        [typeof(Gloves)] = new[] { "Fists", "Grip", "Claw" },
        [typeof(Belt)] = new[] { "Cinch", "Sash", "Buckle" },
        [typeof(Amulet)] = new[] { "Locket", "Talisman", "Charm" },
        [typeof(Ring)] = new[] { "Band", "Loop", "Circlet" },
        [typeof(Relic)] = new[] { "Sigil", "Idol", "Shard" }
    };

    private static readonly string[] _fallbackSecondWords = { "Piece" };

    public static string Generate(Item item)
    {
        string[] secondWords = _secondWordsByType.GetValueOrDefault(item.GetType(), _fallbackSecondWords);
        string first = _firstWords[_random.Next(_firstWords.Length)];
        string second = secondWords[_random.Next(secondWords.Length)];
        return $"{first} {second}";
    }
}
