using Godot;
using System.Collections.Generic;
using System.Linq;

public static class LootGenerator
{
    private static readonly System.Random _random = new();

    // Ring drops are slot-agnostic - CreateItemForSlot maps both LeftRing and RightRing to the
    // same Ring class, so only one appears here to avoid double-weighting rings against every
    // other item type.
    // Weighted rather than flat - Weapon is worth double any other single slot since only one
    // Sentinel (or fewer, character-restricted) can actually use a given dropped weapon, versus
    // armor/accessories which fit any Sentinel. Currency sits in this same pool at one slot's own
    // weight, so "what kind of thing dropped" is one roll, not two.
    private static readonly (EquipmentSlot Slot, int Weight)[] _slotWeights =
    {
        (EquipmentSlot.Weapon, 20),
        (EquipmentSlot.Helmet, 10),
        (EquipmentSlot.Chest, 10),
        (EquipmentSlot.Legplates, 10),
        (EquipmentSlot.Boots, 10),
        (EquipmentSlot.Gloves, 10),
        (EquipmentSlot.Belt, 10),
        (EquipmentSlot.Amulet, 10),
        (EquipmentSlot.LeftRing, 10),
        (EquipmentSlot.Relic, 10),
        (EquipmentSlot.Currency, 10),
        (EquipmentSlot.RuneTome, 10)
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

        item.Initialize(rarity, DefaultItemIcons.Get(item), equippableCharacter, affixes, NameGenerator.Generate(item));

        return item;
    }

    // A fully random item - random droppable slot, random weighted rarity, a random Sentinel if
    // that slot happens to be Weapon. Shared by enemy drops (ItemDropManager) and the vendor's
    // stock generation so both draw from the exact same distribution.
    public static Item GenerateRandomDrop()
    {
        EquipmentSlot slot = WeightedPick(_slotWeights, entry => entry.Weight).Slot;

        return slot switch
        {
            EquipmentSlot.Currency => GenerateCurrencyDrop(),
            EquipmentSlot.RuneTome => GenerateRuneTomeDrop(),
            _ => GenerateGearDrop(slot)
        };
    }

    private static Item GenerateGearDrop(EquipmentSlot slot)
    {
        Rarity rarity = RollRarity();
        CharacterId equippableCharacter = slot == EquipmentSlot.Weapon
            ? RollWeaponCharacter()
            : CharacterId.AnySentinel;

        return Generate(slot, rarity, equippableCharacter);
    }

    // Half the time a dropped weapon favors whichever Sentinel is currently played - immediately
    // usable loot lands more often than a flat 1-in-4 chance would give, without ever guaranteeing it.
    private static CharacterId RollWeaponCharacter()
    {
        CharacterId current = PlayerSentinels.Get(PlayerSentinels.Local);

        if (_random.NextDouble() < 0.5)
        {
            return current;
        }

        CharacterId[] others = _sentinels.Where(id => id != current).ToArray();
        return others[_random.Next(others.Length)];
    }

    // No affixes, no tier/rarity roll - Currency skips the gear pipeline entirely.
    private static Item GenerateCurrencyDrop()
    {
        Currency currency = new GenericCurrency();
        currency.Initialize(Rarity.Common, DefaultItemIcons.Get(currency), CharacterId.AnySentinel, new Godot.Collections.Array<RolledAffix>());
        return currency;
    }

    // Rarities with nothing left to teach just aren't in the pool - no fallback needed there. Only
    // falls back to a gear roll in the rare case every rarity is fully known.
    private static Item GenerateRuneTomeDrop()
    {
        List<(Rarity Rarity, int Weight)> availableRarities = _rarityWeights
            .Where(entry => RuneCatalog.AllOfRarity(entry.Rarity).Any(rune => !IsRuneKnown(rune)))
            .ToList();

        if (availableRarities.Count == 0)
        {
            EquipmentSlot fallbackSlot = WeightedPick(
                _slotWeights.Where(e => e.Slot != EquipmentSlot.Currency && e.Slot != EquipmentSlot.RuneTome).ToList(),
                e => e.Weight).Slot;
            return GenerateGearDrop(fallbackSlot);
        }

        Rarity rarity = WeightedPick(availableRarities, entry => entry.Weight).Rarity;
        List<Rune> candidates = RuneCatalog.AllOfRarity(rarity).Where(rune => !IsRuneKnown(rune)).ToList();
        Rune chosen = candidates[_random.Next(candidates.Count)];

        RuneTome tome = new RuneTome();
        tome.SetTaughtRune(chosen);
        tome.Initialize(rarity, DefaultItemIcons.Get(tome), CharacterId.AnySentinel, new Godot.Collections.Array<RolledAffix>());
        return tome;
    }

    private static bool IsRuneKnown(Rune rune) => SaveManager.Instance.Data.KnownRunes.Any(entry => entry.Rune == rune);

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
