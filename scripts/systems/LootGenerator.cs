using Godot;
using System.Collections.Generic;
using System.Linq;

public static class LootGenerator
{
    private static readonly System.Random _random = new();

    // How good the best tier a given Rarity can roll is - each step up keeps every weaker tier
    // available too (Rare can still roll a T5, it's just not capped there anymore).
    private static readonly Dictionary<Rarity, int> _bestTierByRarity = new()
    {
        [Rarity.Common] = 5,
        [Rarity.Uncommon] = 4,
        [Rarity.Rare] = 3,
        [Rarity.Legendary] = 2,
        [Rarity.Mythic] = 1
    };

    // Rarity.Unique isn't handled here - uniques are hand-authored per item (their own signature
    // AffixType.Unique mod), not randomly generated through this pool. equippableCharacter is the
    // caller's call, not guessed here - a Weapon drop can belong to any Sentinel, played or not.
    public static Item Generate(EquipmentSlot slot, Rarity rarity, CharacterId equippableCharacter = CharacterId.AnySentinel)
    {
        Item item = CreateItemForSlot(slot);
        int bestTier = _bestTierByRarity[rarity];

        List<AffixTierEntry> prefixPool = AffixTable.GetTierEntries(slot, AffixType.Prefix, bestTier).ToList();
        List<AffixTierEntry> suffixPool = AffixTable.GetTierEntries(slot, AffixType.Suffix, bestTier).ToList();

        (int prefixCount, int suffixCount) = RollAffixCounts();

        Godot.Collections.Array<RolledAffix> affixes = new();

        RollAffixes(prefixPool, prefixCount, affixes);
        RollAffixes(suffixPool, suffixCount, affixes);

        item.Initialize(rarity, DefaultItemIcons.Get(slot), equippableCharacter, affixes);

        return item;
    }

    private static Item CreateItemForSlot(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Weapon => new Weapon(),
        EquipmentSlot.Helmet => new Helmet(),
        EquipmentSlot.Chest => new Chest(),
        EquipmentSlot.Legs => new Legs(),
        EquipmentSlot.Boots => new Boots(),
        EquipmentSlot.Gloves => new Gloves(),
        EquipmentSlot.Belt => new Belt(),
        EquipmentSlot.Amulet => new Amulet(),
        EquipmentSlot.LeftRing => new Ring(),
        EquipmentSlot.RightRing => new Ring(),
        EquipmentSlot.Relic => new Relic(),
        _ => throw new System.ArgumentOutOfRangeException(nameof(slot), slot, null)
    };

    // Total mods: 3-6, at least 1 prefix and 1 suffix, neither exceeding 3.
    private static (int prefixCount, int suffixCount) RollAffixCounts()
    {
        int total = _random.Next(3, 7);
        int minPrefix = Mathf.Max(1, total - 3);
        int maxPrefix = Mathf.Min(3, total - 1);
        int prefixCount = _random.Next(minPrefix, maxPrefix + 1);
        return (prefixCount, total - prefixCount);
    }

    // One weighted draw per mod from the flat (affix, tier) pool, same shape PoE itself uses -
    // once an affix is drawn, every tier of that same affix is removed so it can't roll twice.
    private static void RollAffixes(List<AffixTierEntry> pool, int count, Godot.Collections.Array<RolledAffix> results)
    {
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            AffixTierEntry entry = WeightedPick(pool, e => e.Tier.Weight);

            float value = entry.Tier.MinValue + (float)_random.NextDouble() * (entry.Tier.MaxValue - entry.Tier.MinValue);
            value = Mathf.Round(value * 100f) / 100f;

            results.Add(new RolledAffix { Stat = entry.Affix.Stat, Tier = entry.Tier.Tier, Value = value });

            pool.RemoveAll(e => e.Affix.Stat == entry.Affix.Stat);
        }
    }

    private static T WeightedPick<T>(IReadOnlyList<T> items, System.Func<T, int> weightSelector)
    {
        int totalWeight = items.Sum(weightSelector);
        int roll = _random.Next(totalWeight);
        int cumulative = 0;

        foreach (T item in items)
        {
            cumulative += weightSelector(item);
            if (roll < cumulative)
            {
                return item;
            }
        }

        return items[^1];
    }
}
