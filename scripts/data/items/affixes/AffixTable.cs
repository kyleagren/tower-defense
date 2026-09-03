using System.Collections.Generic;

// The full mod pool, hardcoded rather than authored as assets - there are only a few dozen of
// these at most, they're never touched by anything but us, and having them all in one file beats
// clicking through two dozen .tres files to see the whole pool at once.
public static class AffixTable
{
    private static readonly Dictionary<StatId, Affix> _affixes = new()
    {
        // Ritual (Jewelry-bound)
        [StatId.RitualRange] = new Affix
        {
            Name = "Reach",
            DescriptionTemplate = "+{0}% Ritual Range",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualRange,
            TierTable =
            [
                new PercentTierRange(5, 5, 8),
                new PercentTierRange(4, 9, 12),
                new PercentTierRange(3, 13, 17),
                new PercentTierRange(2, 18, 23),
                new PercentTierRange(1, 24, 30)
            ]
        },
        [StatId.RitualCastSpeed] = new Affix
        {
            Name = "Haste",
            DescriptionTemplate = "+{0}% Cast Speed",
            AffixType = AffixType.Suffix,
            Stat = StatId.RitualCastSpeed,
            TierTable =
            [
                new PercentTierRange(5, 4, 6),
                new PercentTierRange(4, 7, 9),
                new PercentTierRange(3, 10, 13),
                new PercentTierRange(2, 14, 18),
                new PercentTierRange(1, 19, 24)
            ]
        },
        [StatId.RitualCooldownReduction] = new Affix
        {
            Name = "Recovery",
            DescriptionTemplate = "+{0}% Cooldown Reduction",
            AffixType = AffixType.Suffix,
            Stat = StatId.RitualCooldownReduction,
            TierTable =
            [
                new PercentTierRange(5, 3, 5),
                new PercentTierRange(4, 6, 8),
                new PercentTierRange(3, 9, 12),
                new PercentTierRange(2, 13, 16),
                new PercentTierRange(1, 17, 20)
            ]
        },
        [StatId.RitualManaCostReduction] = new Affix
        {
            Name = "Efficiency",
            DescriptionTemplate = "-{0}% Mana Cost",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualManaCostReduction,
            TierTable =
            [
                new PercentTierRange(5, 3, 5),
                new PercentTierRange(4, 6, 8),
                new PercentTierRange(3, 9, 11),
                new PercentTierRange(2, 12, 15),
                new PercentTierRange(1, 16, 20)
            ]
        },
        [StatId.RitualCapacityCostReduction] = new Affix
        {
            Name = "Lightweight",
            DescriptionTemplate = "-{0}% Capacity Cost",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualCapacityCostReduction,
            TierTable =
            [
                new PercentTierRange(5, 3, 5),
                new PercentTierRange(4, 6, 8),
                new PercentTierRange(3, 9, 11),
                new PercentTierRange(2, 12, 15),
                new PercentTierRange(1, 16, 20)
            ]
        },
        [StatId.RitualDamage] = new Affix
        {
            Name = "Potency",
            DescriptionTemplate = "+{0}% Ritual Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualDamage,
            TierTable =
            [
                new PercentTierRange(5, 6, 10),
                new PercentTierRange(4, 11, 15),
                new PercentTierRange(3, 16, 20),
                new PercentTierRange(2, 21, 27),
                new PercentTierRange(1, 28, 35)
            ]
        },
        [StatId.RitualFireDamage] = new Affix
        {
            Name = "Engulfing",
            DescriptionTemplate = "+{0}% Fire Ritual Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualFireDamage,
            TierTable =
            [
                new PercentTierRange(5, 12, 20),
                new PercentTierRange(4, 22, 30),
                new PercentTierRange(3, 32, 40),
                new PercentTierRange(2, 42, 54),
                new PercentTierRange(1, 56, 70)
            ]
        },
        [StatId.RitualWaterDamage] = new Affix
        {
            Name = "Drowning",
            DescriptionTemplate = "+{0}% Water Ritual Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualWaterDamage,
            TierTable =
            [
                new PercentTierRange(5, 12, 20),
                new PercentTierRange(4, 22, 30),
                new PercentTierRange(3, 32, 40),
                new PercentTierRange(2, 42, 54),
                new PercentTierRange(1, 56, 70)
            ]
        },
        [StatId.RitualColdDamage] = new Affix
        {
            Name = "Freezing",
            DescriptionTemplate = "+{0}% Cold Ritual Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualColdDamage,
            TierTable =
            [
                new PercentTierRange(5, 12, 20),
                new PercentTierRange(4, 22, 30),
                new PercentTierRange(3, 32, 40),
                new PercentTierRange(2, 42, 54),
                new PercentTierRange(1, 56, 70)
            ]
        },
        [StatId.RitualLightningDamage] = new Affix
        {
            Name = "Shocking",
            DescriptionTemplate = "+{0}% Lightning Ritual Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualLightningDamage,
            TierTable =
            [
                new PercentTierRange(5, 12, 20),
                new PercentTierRange(4, 22, 30),
                new PercentTierRange(3, 32, 40),
                new PercentTierRange(2, 42, 54),
                new PercentTierRange(1, 56, 70)
            ]
        },
        [StatId.RitualWindDamage] = new Affix
        {
            Name = "Howling",
            DescriptionTemplate = "+{0}% Wind Ritual Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualWindDamage,
            TierTable =
            [
                new PercentTierRange(5, 12, 20),
                new PercentTierRange(4, 22, 30),
                new PercentTierRange(3, 32, 40),
                new PercentTierRange(2, 42, 54),
                new PercentTierRange(1, 56, 70)
            ]
        },

        // Sentinel (Armor-bound)
        [StatId.SentinelMaxHealth] = new Affix
        {
            Name = "Vitality",
            DescriptionTemplate = "+{0}% Max Health",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelMaxHealth,
            TierTable =
            [
                new PercentTierRange(5, 5, 8),
                new PercentTierRange(4, 9, 13),
                new PercentTierRange(3, 14, 19),
                new PercentTierRange(2, 20, 26),
                new PercentTierRange(1, 27, 35)
            ]
        },
        [StatId.SentinelMoveSpeed] = new Affix
        {
            Name = "Swift",
            DescriptionTemplate = "+{0}% Move Speed",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelMoveSpeed,
            TierTable =
            [
                new PercentTierRange(5, 3, 5),
                new PercentTierRange(4, 6, 8),
                new PercentTierRange(3, 9, 11),
                new PercentTierRange(2, 12, 15),
                new PercentTierRange(1, 16, 20)
            ]
        },
        [StatId.SentinelArmor] = new Affix
        {
            Name = "Fortitude",
            DescriptionTemplate = "+{0}% Armor",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelArmor,
            TierTable =
            [
                new PercentTierRange(5, 3, 5),
                new PercentTierRange(4, 6, 8),
                new PercentTierRange(3, 9, 12),
                new PercentTierRange(2, 13, 17),
                new PercentTierRange(1, 18, 22)
            ]
        },
        [StatId.SentinelShield] = new Affix
        {
            Name = "Aegis",
            DescriptionTemplate = "+{0}% Shield",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelShield,
            TierTable =
            [
                new PercentTierRange(5, 5, 8),
                new PercentTierRange(4, 9, 13),
                new PercentTierRange(3, 14, 19),
                new PercentTierRange(2, 20, 26),
                new PercentTierRange(1, 27, 35)
            ]
        },
        [StatId.SentinelHealthRegen] = new Affix
        {
            Name = "Renewal",
            DescriptionTemplate = "+{0}% Health Regeneration",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelHealthRegen,
            TierTable =
            [
                new PercentTierRange(5, 5, 8),
                new PercentTierRange(4, 9, 13),
                new PercentTierRange(3, 14, 19),
                new PercentTierRange(2, 20, 26),
                new PercentTierRange(1, 27, 35)
            ]
        },
        [StatId.SentinelCritChance] = new Affix
        {
            Name = "Precision",
            DescriptionTemplate = "+{0}% Critical Strike Chance",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelCritChance,
            TierTable =
            [
                new PercentTierRange(5, 2, 3),
                new PercentTierRange(4, 4, 5),
                new PercentTierRange(3, 6, 8),
                new PercentTierRange(2, 9, 11),
                new PercentTierRange(1, 12, 15)
            ]
        },
        [StatId.SentinelCritDamage] = new Affix
        {
            Name = "Brutality",
            DescriptionTemplate = "+{0}% Critical Strike Damage",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelCritDamage,
            TierTable =
            [
                new PercentTierRange(5, 15, 25),
                new PercentTierRange(4, 26, 35),
                new PercentTierRange(3, 36, 50),
                new PercentTierRange(2, 51, 70),
                new PercentTierRange(1, 71, 100)
            ]
        },
        [StatId.SentinelCooldownReduction] = new Affix
        {
            Name = "Celerity",
            DescriptionTemplate = "+{0}% Ability Cooldown Reduction",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelCooldownReduction,
            TierTable =
            [
                new PercentTierRange(5, 3, 5),
                new PercentTierRange(4, 6, 8),
                new PercentTierRange(3, 9, 12),
                new PercentTierRange(2, 13, 16),
                new PercentTierRange(1, 17, 20)
            ]
        },

        // Weapon-bound
        [StatId.WeaponAttackDamage] = new Affix
        {
            Name = "Might",
            DescriptionTemplate = "+{0}% Attack Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.WeaponAttackDamage,
            TierTable =
            [
                new PercentTierRange(5, 5, 8),
                new PercentTierRange(4, 9, 13),
                new PercentTierRange(3, 14, 19),
                new PercentTierRange(2, 20, 26),
                new PercentTierRange(1, 27, 35)
            ]
        }
    };

    public static Affix Get(StatId stat)
    {
        return _affixes[stat];
    }
}
