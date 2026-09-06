using System.Collections.Generic;
using System.Linq;

// The crafting table's mod-editing rules: add or remove a random prefix/suffix, or reroll every
// existing mod's value within its own tier's range. Shares LootGenerator's weighted-pick and
// tier-range rules so a crafted mod could just as easily have come from a drop.
public static class CraftingActions
{
    private static readonly System.Random _random = new();

    public static bool CanAddAffix(Item item, AffixType type)
    {
        return CountOf(item, type) < MaxFor(type) && EligiblePool(item, type).Any();
    }

    public static bool CanRemoveAffix(Item item, AffixType type)
    {
        return CountOf(item, type) > 0;
    }

    public static bool CanReroll(Item item)
    {
        return item.Affixes.Count > 0;
    }

    public static void AddAffix(Item item, AffixType type)
    {
        List<AffixTierEntry> pool = EligiblePool(item, type).ToList();
        AffixTierEntry entry = LootGenerator.WeightedPick(pool, e => e.Tier.Weight);
        item.Affixes.Add(new RolledAffix { Stat = entry.Affix.Stat, Tier = entry.Tier.Tier, Value = entry.Tier.RollValue(_random) });
    }

    // Random removal by design - the player doesn't pick which mod goes.
    public static void RemoveRandomAffix(Item item, AffixType type)
    {
        List<RolledAffix> candidates = item.Affixes.Where(roll => roll.Affix.AffixType == type).ToList();
        item.Affixes.Remove(candidates[_random.Next(candidates.Count)]);
    }

    public static void RerollAll(Item item)
    {
        foreach (RolledAffix roll in item.Affixes)
        {
            roll.Value = roll.TierRange.RollValue(_random);
        }
    }

    private static int CountOf(Item item, AffixType type) => item.Affixes.Count(roll => roll.Affix.AffixType == type);

    private static int MaxFor(AffixType type) => type == AffixType.Prefix ? GameRules.MaxPrefixAffixes : GameRules.MaxSuffixAffixes;

    private static IEnumerable<AffixTierEntry> EligiblePool(Item item, AffixType type)
    {
        int bestTier = LootGenerator.BestTierFor(item.Rarity);
        HashSet<StatId> existing = item.Affixes.Select(roll => roll.Stat).ToHashSet();

        return AffixTable.GetTierEntries(item.Slot, type, bestTier, item.EquippableCharacter)
            .Where(entry => !existing.Contains(entry.Affix.Stat));
    }
}
