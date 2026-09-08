using Godot;
using System.Collections.Generic;
using System.Linq;

public static class LootGenerator
{
    private static readonly System.Random _random = new();

    // Ring drops are slot-agnostic - CreateItemForSlot maps both LeftRing and RightRing to the
    // same Ring class, so only one appears here to avoid double-weighting rings against every
    // other item type. Moved here from ItemDropManager so vendor stock draws from the identical
    // pool instead of a second hand-kept copy.
    private static readonly EquipmentSlot[] _droppableSlots =
    {
        EquipmentSlot.Weapon,
        EquipmentSlot.Helmet,
        EquipmentSlot.Chest,
        EquipmentSlot.Legplates,
        EquipmentSlot.Boots,
        EquipmentSlot.Gloves,
        EquipmentSlot.Belt,
        EquipmentSlot.Amulet,
        EquipmentSlot.LeftRing,
        EquipmentSlot.Relic
    };

    private static readonly CharacterId[] _sentinels =
        { CharacterId.Soldier, CharacterId.Tank, CharacterId.Assassin, CharacterId.Templar };

    // Common is far likelier than Legendary. Rarity.Unique is never rolled here, matching this
    // file's own rule elsewhere that uniques are hand-authored, not generated.
    private static readonly (Rarity Rarity, int Weight)[] _rarityWeights =
    {
        (Rarity.Common, 100),
        (Rarity.Uncommon, 45),
        (Rarity.Rare, 18),
        (Rarity.Epic, 6),
        (Rarity.Legendary, 1)
    };

    // How good the best tier a given Rarity can roll is - each step up keeps every weaker tier
    // available too (Rare can still roll a T1, it's just not capped there anymore). Tier 1 is the
    // weakest and ascending numbers are stronger (see AffixTierRange.Tier), so this is each
    // Rarity's ceiling, not its floor.
    private static readonly Dictionary<Rarity, int> _bestTierByRarity = new()
    {
        [Rarity.Common] = 1,
        [Rarity.Uncommon] = 2,
        [Rarity.Rare] = 3,
        [Rarity.Epic] = 4,
        [Rarity.Legendary] = 5
    };

    // Rarity.Unique isn't handled here - uniques are hand-authored per item (their own signature
    // AffixType.Unique mod), not randomly generated through this pool. equippableCharacter is the
    // caller's call, not guessed here - a Weapon drop can belong to any Sentinel, played or not.
    public static Item Generate(EquipmentSlot slot, Rarity rarity, CharacterId equippableCharacter = CharacterId.AnySentinel)
    {
        Item item = CreateItemForSlot(slot, equippableCharacter);

        if (item is Armor armor)
        {
            armor.RollBaseStats(rarity, _random);
        }

        if (item is Weapon weapon)
        {
            weapon.RollBaseStats(rarity);
        }

        int bestTier = _bestTierByRarity[rarity];

        List<AffixTierEntry> prefixPool = AffixTable.GetTierEntries(item, AffixType.Prefix, bestTier).ToList();
        List<AffixTierEntry> suffixPool = AffixTable.GetTierEntries(item, AffixType.Suffix, bestTier).ToList();

        (int prefixCount, int suffixCount) = RollAffixCounts();

        Godot.Collections.Array<RolledAffix> affixes = new();

        RollAffixes(prefixPool, prefixCount, affixes);
        RollAffixes(suffixPool, suffixCount, affixes);

        item.Initialize(rarity, DefaultItemIcons.Get(item), equippableCharacter, affixes);

        return item;
    }

    // A fully random item - random droppable slot, random weighted rarity, a random Sentinel if
    // that slot happens to be Weapon. Shared by enemy drops (ItemDropManager) and the vendor's
    // stock generation so both draw from the exact same distribution.
    public static Item GenerateRandomDrop()
    {
        EquipmentSlot slot = _droppableSlots[_random.Next(_droppableSlots.Length)];
        Rarity rarity = RollRarity();
        CharacterId equippableCharacter = slot == EquipmentSlot.Weapon
            ? _sentinels[_random.Next(_sentinels.Length)]
            : CharacterId.AnySentinel;

        return Generate(slot, rarity, equippableCharacter);
    }

    private static Rarity RollRarity()
    {
        int totalWeight = 0;
        foreach ((Rarity _, int weight) in _rarityWeights)
        {
            totalWeight += weight;
        }

        int roll = _random.Next(totalWeight);
        int cumulative = 0;

        foreach ((Rarity rarity, int weight) in _rarityWeights)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                return rarity;
            }
        }

        return Rarity.Common;
    }

    private static Item CreateItemForSlot(EquipmentSlot slot, CharacterId equippableCharacter) => slot switch
    {
        EquipmentSlot.Weapon => equippableCharacter == CharacterId.Tank ? new SwordAndShield() : new Gun(),
        EquipmentSlot.Helmet => new Helmet(),
        EquipmentSlot.Chest => new Chest(),
        EquipmentSlot.Legplates => new Legplates(),
        EquipmentSlot.Boots => new Boots(),
        EquipmentSlot.Gloves => new Gloves(),
        EquipmentSlot.Belt => new Belt(),
        EquipmentSlot.Amulet => new Amulet(),
        EquipmentSlot.LeftRing => new Ring(),
        EquipmentSlot.RightRing => new Ring(),
        EquipmentSlot.Relic => new Relic(),
        _ => throw new System.ArgumentOutOfRangeException(nameof(slot), slot, null)
    };

    // Total mods: 3-6, at least 1 prefix and 1 suffix, neither exceeding GameRules' per-type max.
    private static (int prefixCount, int suffixCount) RollAffixCounts()
    {
        int total = _random.Next(3, GameRules.MaxPrefixAffixes + GameRules.MaxSuffixAffixes + 1);
        int minPrefix = Mathf.Max(1, total - GameRules.MaxSuffixAffixes);
        int maxPrefix = Mathf.Min(GameRules.MaxPrefixAffixes, total - 1);
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

            results.Add(new RolledAffix { Stat = entry.Affix.Stat, Tier = entry.Tier.Tier, Value = entry.Tier.RollValue(_random) });

            pool.RemoveAll(e => e.Affix.Stat == entry.Affix.Stat);
        }
    }

    // Shared with the crafting table's "add a mod" action, so both draw from the exact same
    // rarity-ceiling rule instead of a second copy of this table.
    public static int BestTierFor(Rarity rarity) => _bestTierByRarity[rarity];

    public static T WeightedPick<T>(IReadOnlyList<T> items, System.Func<T, int> weightSelector)
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
