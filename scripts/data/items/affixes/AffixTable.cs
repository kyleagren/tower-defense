using System.Collections.Generic;
using System.Linq;

// The full mod pool, hardcoded rather than authored as assets - there are only a few dozen of
// these at most, they're never touched by anything but us, and having them all in one file beats
// clicking through two dozen .tres files to see the whole pool at once.
public static class AffixTable
{
    private static readonly EquipmentSlot[] AccessorySlots =
        { EquipmentSlot.Belt, EquipmentSlot.Amulet, EquipmentSlot.LeftRing, EquipmentSlot.RightRing, EquipmentSlot.Relic };

    private static readonly EquipmentSlot[] ArmorSlots =
        { EquipmentSlot.Helmet, EquipmentSlot.Chest, EquipmentSlot.Legs, EquipmentSlot.Boots, EquipmentSlot.Gloves };

    private static readonly EquipmentSlot[] WeaponSlots = { EquipmentSlot.Weapon };

    private static readonly Dictionary<StatId, Affix> _affixes = new()
    {
        // Ritual (Accessory-bound)
        [StatId.RitualRange] = new Affix
        {
            Name = "Reach",
            DescriptionTemplate = "+{0}% Ritual Range",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualRange,
            AllowedSlots = AccessorySlots,
            TierTable =
            [
                new PercentTierRange(1, 5, 8),
                new PercentTierRange(2, 9, 12),
                new PercentTierRange(3, 13, 17),
                new PercentTierRange(4, 18, 23),
                new PercentTierRange(5, 24, 30)
            ]
        },
        [StatId.RitualCastSpeed] = new Affix
        {
            Name = "Haste",
            DescriptionTemplate = "+{0}% Cast Speed",
            AffixType = AffixType.Suffix,
            Stat = StatId.RitualCastSpeed,
            AllowedSlots = AccessorySlots,
            TierTable =
            [
                new PercentTierRange(1, 4, 6),
                new PercentTierRange(2, 7, 9),
                new PercentTierRange(3, 10, 13),
                new PercentTierRange(4, 14, 18),
                new PercentTierRange(5, 19, 24)
            ]
        },
        [StatId.RitualCooldownRecoveryRate] = new Affix
        {
            Name = "Recovery",
            DescriptionTemplate = "+{0}% Cooldown Recovery Rate",
            AffixType = AffixType.Suffix,
            Stat = StatId.RitualCooldownRecoveryRate,
            AllowedSlots = AccessorySlots,
            TierTable =
            [
                new PercentTierRange(1, 3, 5),
                new PercentTierRange(2, 6, 8),
                new PercentTierRange(3, 9, 12),
                new PercentTierRange(4, 13, 16),
                new PercentTierRange(5, 17, 20)
            ]
        },
        [StatId.RitualManaCostReduction] = new Affix
        {
            Name = "Efficiency",
            DescriptionTemplate = "-{0}% Mana Cost",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualManaCostReduction,
            AllowedSlots = AccessorySlots,
            TierTable =
            [
                new PercentTierRange(1, 3, 5),
                new PercentTierRange(2, 6, 8),
                new PercentTierRange(3, 9, 11),
                new PercentTierRange(4, 12, 15),
                new PercentTierRange(5, 16, 20)
            ]
        },
        [StatId.RitualCapacityCostReduction] = new Affix
        {
            Name = "Lightweight",
            DescriptionTemplate = "-{0}% Capacity Cost",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualCapacityCostReduction,
            AllowedSlots = AccessorySlots,
            TierTable =
            [
                new PercentTierRange(1, 3, 5),
                new PercentTierRange(2, 6, 8),
                new PercentTierRange(3, 9, 11),
                new PercentTierRange(4, 12, 15),
                new PercentTierRange(5, 16, 20)
            ]
        },
        [StatId.RitualDamage] = new Affix
        {
            Name = "Potency",
            DescriptionTemplate = "+{0}% Ritual Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualDamage,
            AllowedSlots = AccessorySlots,
            TierTable =
            [
                new PercentTierRange(1, 6, 10),
                new PercentTierRange(2, 11, 15),
                new PercentTierRange(3, 16, 20),
                new PercentTierRange(4, 21, 27),
                new PercentTierRange(5, 28, 35)
            ]
        },
        [StatId.RitualFireDamage] = new Affix
        {
            Name = "Engulfing",
            DescriptionTemplate = "+{0}% Fire Ritual Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualFireDamage,
            AllowedSlots = AccessorySlots,
            TierTable =
            [
                new PercentTierRange(1, 12, 20),
                new PercentTierRange(2, 22, 30),
                new PercentTierRange(3, 32, 40),
                new PercentTierRange(4, 42, 54),
                new PercentTierRange(5, 56, 70)
            ]
        },
        [StatId.RitualWaterDamage] = new Affix
        {
            Name = "Drowning",
            DescriptionTemplate = "+{0}% Water Ritual Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualWaterDamage,
            AllowedSlots = AccessorySlots,
            TierTable =
            [
                new PercentTierRange(1, 12, 20),
                new PercentTierRange(2, 22, 30),
                new PercentTierRange(3, 32, 40),
                new PercentTierRange(4, 42, 54),
                new PercentTierRange(5, 56, 70)
            ]
        },
        [StatId.RitualColdDamage] = new Affix
        {
            Name = "Freezing",
            DescriptionTemplate = "+{0}% Cold Ritual Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualColdDamage,
            AllowedSlots = AccessorySlots,
            TierTable =
            [
                new PercentTierRange(1, 12, 20),
                new PercentTierRange(2, 22, 30),
                new PercentTierRange(3, 32, 40),
                new PercentTierRange(4, 42, 54),
                new PercentTierRange(5, 56, 70)
            ]
        },
        [StatId.RitualLightningDamage] = new Affix
        {
            Name = "Shocking",
            DescriptionTemplate = "+{0}% Lightning Ritual Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualLightningDamage,
            AllowedSlots = AccessorySlots,
            TierTable =
            [
                new PercentTierRange(1, 12, 20),
                new PercentTierRange(2, 22, 30),
                new PercentTierRange(3, 32, 40),
                new PercentTierRange(4, 42, 54),
                new PercentTierRange(5, 56, 70)
            ]
        },
        [StatId.RitualWindDamage] = new Affix
        {
            Name = "Howling",
            DescriptionTemplate = "+{0}% Wind Ritual Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.RitualWindDamage,
            AllowedSlots = AccessorySlots,
            TierTable =
            [
                new PercentTierRange(1, 12, 20),
                new PercentTierRange(2, 22, 30),
                new PercentTierRange(3, 32, 40),
                new PercentTierRange(4, 42, 54),
                new PercentTierRange(5, 56, 70)
            ]
        },

        // Sentinel (Armor-bound)
        [StatId.SentinelMaxHealth] = new Affix
        {
            Name = "Vitality",
            DescriptionTemplate = "+{0}% Max Health",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelMaxHealth,
            AllowedSlots = ArmorSlots,
            TierTable =
            [
                new PercentTierRange(1, 5, 8),
                new PercentTierRange(2, 9, 13),
                new PercentTierRange(3, 14, 19),
                new PercentTierRange(4, 20, 26),
                new PercentTierRange(5, 27, 35)
            ]
        },
        [StatId.SentinelMoveSpeed] = new Affix
        {
            Name = "Swift",
            DescriptionTemplate = "+{0}% Move Speed",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelMoveSpeed,
            AllowedSlots = ArmorSlots,
            TierTable =
            [
                new PercentTierRange(1, 3, 5),
                new PercentTierRange(2, 6, 8),
                new PercentTierRange(3, 9, 11),
                new PercentTierRange(4, 12, 15),
                new PercentTierRange(5, 16, 20)
            ]
        },
        [StatId.SentinelArmor] = new Affix
        {
            Name = "Fortitude",
            DescriptionTemplate = "+{0}% Armor",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelArmor,
            AllowedSlots = ArmorSlots,
            TierTable =
            [
                new PercentTierRange(1, 3, 5),
                new PercentTierRange(2, 6, 8),
                new PercentTierRange(3, 9, 12),
                new PercentTierRange(4, 13, 17),
                new PercentTierRange(5, 18, 22)
            ]
        },
        [StatId.SentinelShield] = new Affix
        {
            Name = "Aegis",
            DescriptionTemplate = "+{0}% Shield",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelShield,
            AllowedSlots = ArmorSlots,
            TierTable =
            [
                new PercentTierRange(1, 5, 8),
                new PercentTierRange(2, 9, 13),
                new PercentTierRange(3, 14, 19),
                new PercentTierRange(4, 20, 26),
                new PercentTierRange(5, 27, 35)
            ]
        },
        [StatId.SentinelHealthRegen] = new Affix
        {
            Name = "Renewal",
            DescriptionTemplate = "+{0}% Health Regeneration",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelHealthRegen,
            AllowedSlots = ArmorSlots,
            TierTable =
            [
                new PercentTierRange(1, 5, 8),
                new PercentTierRange(2, 9, 13),
                new PercentTierRange(3, 14, 19),
                new PercentTierRange(4, 20, 26),
                new PercentTierRange(5, 27, 35)
            ]
        },
        [StatId.SentinelCritChance] = new Affix
        {
            Name = "Precision",
            DescriptionTemplate = "+{0}% Critical Strike Chance",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelCritChance,
            AllowedSlots = ArmorSlots,
            TierTable =
            [
                new PercentTierRange(1, 2, 3),
                new PercentTierRange(2, 4, 5),
                new PercentTierRange(3, 6, 8),
                new PercentTierRange(4, 9, 11),
                new PercentTierRange(5, 12, 15)
            ]
        },
        [StatId.SentinelCritDamage] = new Affix
        {
            Name = "Brutality",
            DescriptionTemplate = "+{0}% Critical Strike Damage",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelCritDamage,
            AllowedSlots = ArmorSlots,
            TierTable =
            [
                new PercentTierRange(1, 15, 25),
                new PercentTierRange(2, 26, 35),
                new PercentTierRange(3, 36, 50),
                new PercentTierRange(4, 51, 70),
                new PercentTierRange(5, 71, 100)
            ]
        },
        [StatId.SentinelCooldownRecoveryRate] = new Affix
        {
            Name = "Celerity",
            DescriptionTemplate = "+{0}% Ability Cooldown Recovery Rate",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelCooldownRecoveryRate,
            AllowedSlots = ArmorSlots,
            TierTable =
            [
                new PercentTierRange(1, 3, 5),
                new PercentTierRange(2, 6, 8),
                new PercentTierRange(3, 9, 12),
                new PercentTierRange(4, 13, 16),
                new PercentTierRange(5, 17, 20)
            ]
        },

        // Weapon-bound
        [StatId.WeaponAttackDamage] = new Affix
        {
            Name = "Might",
            DescriptionTemplate = "+{0}% Attack Damage",
            AffixType = AffixType.Prefix,
            Stat = StatId.WeaponAttackDamage,
            AllowedSlots = WeaponSlots,
            TierTable =
            [
                new PercentTierRange(1, 5, 8),
                new PercentTierRange(2, 9, 13),
                new PercentTierRange(3, 14, 19),
                new PercentTierRange(4, 20, 26),
                new PercentTierRange(5, 27, 35)
            ]
        }
    };

    public static Affix Get(StatId stat) => _affixes[stat];
    public static IEnumerable<Affix> All => _affixes.Values;

    // The flat weighted pool a roll actually draws from: every eligible affix's eligible tiers,
    // flattened into individual (affix, tier) entries. Mirrors how PoE itself rolls - one weighted
    // draw over every tier of every mod at once, not "pick a mod, then pick its tier" as two steps.
    public static IEnumerable<AffixTierEntry> GetTierEntries(EquipmentSlot slot, AffixType type, int bestTier, CharacterId character)
    {
        return _affixes.Values
            .Where(affix => affix.AffixType == type)
            .Where(affix => affix.AllowedSlots.Length == 0 || affix.AllowedSlots.Contains(slot))
            .Where(affix => affix.IsAvailableTo(character))
            .SelectMany(affix => affix.TierTable
                .Where(tier => tier.Tier <= bestTier)
                .Select(tier => new AffixTierEntry(affix, tier)));
    }
}
