using System.Collections.Generic;
using System.Linq;

// The crafting table's mod-editing rules: add, remove or replace a prefix/suffix, reroll every
// existing mod's value within its own tier's range, or push a random mod up one tier. Shares
// LootGenerator's weighted-pick and tier-range rules so a crafted mod could just as easily have
// come from a drop.
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

    public static bool CanReplaceAffixAt(Item item, AffixType type, int index)
    {
        return index < CountOf(item, type) && ReplacementPool(item, AffixAt(item, type, index)).Any();
    }

    public static bool CanReroll(Item item)
    {
        return item.Affixes.Count > 0;
    }

    public static bool CanUpgradeTier(Item item)
    {
        return item.Affixes.Any(roll => NextTier(item, roll) != null);
    }

    public static void AddAffix(Item item, AffixType type)
    {
        item.Affixes.Add(RollFrom(EligiblePool(item, type)));
    }

    // Random removal by design - the player doesn't pick which mod goes.
    public static void RemoveRandomAffix(Item item, AffixType type)
    {
        List<RolledAffix> candidates = item.Affixes.Where(roll => roll.Affix.AffixType == type).ToList();
        item.Affixes.Remove(candidates[_random.Next(candidates.Count)]);
    }

    // Swapped in place so the replacement keeps the same bar in the crafting panel.
    public static void ReplaceAffixAt(Item item, AffixType type, int index)
    {
        RolledAffix old = AffixAt(item, type, index);
        item.Affixes[item.Affixes.IndexOf(old)] = RollFrom(ReplacementPool(item, old));
    }

    public static void RerollAll(Item item)
    {
        foreach (RolledAffix roll in item.Affixes)
        {
            roll.Value = roll.TierRange.RollValue(_random);
        }
    }

    // Mods already at their ceiling are excluded, so repeated upgrades always converge on every mod
    // sitting at the best tier the item's rarity allows.
    public static void UpgradeRandomTier(Item item)
    {
        List<RolledAffix> candidates = item.Affixes.Where(roll => NextTier(item, roll) != null).ToList();
        RolledAffix roll = candidates[_random.Next(candidates.Count)];
        AffixTierRange next = NextTier(item, roll);

        roll.Tier = next.Tier;
        roll.Value = next.RollValue(_random);
    }

    private static int CountOf(Item item, AffixType type) => item.Affixes.Count(roll => roll.Affix.AffixType == type);

    private static int MaxFor(AffixType type) => type == AffixType.Prefix ? GameRules.MaxPrefixAffixes : GameRules.MaxSuffixAffixes;

    // Index matches the crafting panel's bar order - the Nth affix of that type in item.Affixes.
    private static RolledAffix AffixAt(Item item, AffixType type, int index)
    {
        return item.Affixes.Where(roll => roll.Affix.AffixType == type).ElementAt(index);
    }

    private static RolledAffix RollFrom(IEnumerable<AffixTierEntry> pool)
    {
        AffixTierEntry entry = LootGenerator.WeightedPick(pool.ToList(), e => e.Tier.Weight);
        return new RolledAffix { Stat = entry.Affix.Stat, Tier = entry.Tier.Tier, Value = entry.Tier.RollValue(_random) };
    }

    // `ignoring` is a mod about to be replaced - it shouldn't block its own successors.
    private static IEnumerable<AffixTierEntry> EligiblePool(Item item, AffixType type, RolledAffix ignoring = null)
    {
        int bestTier = LootGenerator.BestTierFor(item.Rarity);

        return AffixTable.GetTierEntries(item, type, bestTier)
            .Where(entry => !item.Affixes.Any(roll => roll != ignoring && roll.Affix.ConflictsWith(entry.Affix)));
    }

    private static IEnumerable<AffixTierEntry> ReplacementPool(Item item, RolledAffix old)
    {
        return EligiblePool(item, old.Affix.AffixType, old).Where(entry => entry.Affix.Stat != old.Stat);
    }

    // The next tier up that still fits under the item's rarity cap, or null if the mod is already there.
    private static AffixTierRange NextTier(Item item, RolledAffix roll)
    {
        int bestTier = LootGenerator.BestTierFor(item.Rarity);

        return roll.Affix.TierTable
            .Where(tier => tier.Tier > roll.Tier && tier.Tier <= bestTier)
            .OrderBy(tier => tier.Tier)
            .FirstOrDefault();
    }
}
