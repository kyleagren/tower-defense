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
        { EquipmentSlot.Helmet, EquipmentSlot.Chest, EquipmentSlot.Legplates, EquipmentSlot.Boots, EquipmentSlot.Gloves };

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
            Tags = [AffixTag.Range],
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
            Tags = [AffixTag.Speed],
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
            Tags = [AffixTag.Cooldown],
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
            Tags = [AffixTag.Cost],
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
            Tags = [AffixTag.Cost],
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
            Tags = [AffixTag.Damage],
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
            Tags = [AffixTag.Damage, AffixTag.Fire],
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
            Tags = [AffixTag.Damage, AffixTag.Water],
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
            Tags = [AffixTag.Damage, AffixTag.Cold],
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
            Tags = [AffixTag.Damage, AffixTag.Lightning],
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
            Tags = [AffixTag.Damage, AffixTag.Wind],
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
            Tags = [AffixTag.Health],
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
            Tags = [AffixTag.Speed],
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
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelArmor,
            AllowedSlots = ArmorSlots,
            Tags = [AffixTag.Defences],
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
            Tags = [AffixTag.Defences],
            TierTable =
            [
                new PercentTierRange(1, 5, 8),
                new PercentTierRange(2, 9, 13),
                new PercentTierRange(3, 14, 19),
                new PercentTierRange(4, 20, 26),
                new PercentTierRange(5, 27, 35)
            ]
        },
        [StatId.SentinelArmorAndShield] = new Affix
        {
            Name = "Vanguard",
            DescriptionTemplate = "+{0}% Armor and Shield",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelArmorAndShield,
            AllowedSlots = ArmorSlots,
            Tags = [AffixTag.Defences],
            TierTable =
            [
                new PercentTierRange(1, 3, 5),
                new PercentTierRange(2, 6, 8),
                new PercentTierRange(3, 9, 12),
                new PercentTierRange(4, 13, 17),
                new PercentTierRange(5, 18, 22)
            ]
        },
        [StatId.SentinelTotalArmor] = new Affix
        {
            Name = "Bulwark",
            DescriptionTemplate = "+{0}% Total Armor",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelTotalArmor,
            AllowedSlots = AccessorySlots,
            Tags = [AffixTag.Defences],
            TierTable =
            [
                new PercentTierRange(1, 3, 5),
                new PercentTierRange(2, 6, 8),
                new PercentTierRange(3, 9, 12),
                new PercentTierRange(4, 13, 17),
                new PercentTierRange(5, 18, 22)
            ]
        },
        [StatId.SentinelTotalShield] = new Affix
        {
            Name = "Ward",
            DescriptionTemplate = "+{0}% Total Shield",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelTotalShield,
            AllowedSlots = AccessorySlots,
            Tags = [AffixTag.Defences],
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
            Tags = [AffixTag.Health],
            TierTable =
            [
                new PercentTierRange(1, 5, 8),
                new PercentTierRange(2, 9, 13),
                new PercentTierRange(3, 14, 19),
                new PercentTierRange(4, 20, 26),
                new PercentTierRange(5, 27, 35)
            ]
        },
        [StatId.SentinelBaseHealthRegen] = new Affix
        {
            Name = "Mending",
            DescriptionTemplate = "+{0} Health Regenerated per Second",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelBaseHealthRegen,
            AllowedSlots = ArmorSlots,
            Tags = [AffixTag.Health],
            IsFlatValue = true,
            TierTable =
            [
                new FlatTierRange(1, 1, 1),
                new FlatTierRange(2, 2, 2),
                new FlatTierRange(3, 3, 4),
                new FlatTierRange(4, 5, 6),
                new FlatTierRange(5, 7, 9)
            ]
        },
        [StatId.SentinelCritChance] = new Affix
        {
            Name = "Precision",
            DescriptionTemplate = "+{0}% Critical Strike Chance",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelCritChance,
            AllowedSlots = ArmorSlots,
            Tags = [AffixTag.Critical],
            TierTable =
            [
                new PercentTierRange(1, 10, 15),
                new PercentTierRange(2, 16, 22),
                new PercentTierRange(3, 23, 30),
                new PercentTierRange(4, 31, 40),
                new PercentTierRange(5, 41, 55)
            ]
        },
        [StatId.SentinelBaseCritChance] = new Affix
        {
            Name = "Edge",
            DescriptionTemplate = "+{0}% to Base Critical Strike Chance",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelBaseCritChance,
            AllowedSlots = WeaponSlots,
            Tags = [AffixTag.Critical],
            TierTable =
            [
                new PercentTierRange(1, 1, 2),
                new PercentTierRange(2, 3, 4),
                new PercentTierRange(3, 5, 6),
                new PercentTierRange(4, 7, 9),
                new PercentTierRange(5, 10, 12)
            ]
        },
        [StatId.SentinelBaseAttackSpeed] = new Affix
        {
            Name = "Swiftness",
            DescriptionTemplate = "+{0}% to Base Attack Speed",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelBaseAttackSpeed,
            AllowedSlots = WeaponSlots,
            Tags = [AffixTag.Speed],
            TierTable =
            [
                new PercentTierRange(1, 4, 6),
                new PercentTierRange(2, 7, 9),
                new PercentTierRange(3, 10, 13),
                new PercentTierRange(4, 14, 18),
                new PercentTierRange(5, 19, 24)
            ]
        },
        [StatId.SentinelCritDamage] = new Affix
        {
            Name = "Brutality",
            DescriptionTemplate = "+{0}% Critical Strike Damage",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelCritDamage,
            AllowedSlots = ArmorSlots,
            Tags = [AffixTag.Critical, AffixTag.Damage],
            TierTable =
            [
                new PercentTierRange(1, 15, 25),
                new PercentTierRange(2, 26, 35),
                new PercentTierRange(3, 36, 50),
                new PercentTierRange(4, 51, 70),
                new PercentTierRange(5, 71, 100)
            ]
        },
        [StatId.SentinelMoreCritDamageBonus] = new Affix
        {
            Name = "Ruin",
            DescriptionTemplate = "{0}% more Critical Damage Bonus",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelMoreCritDamageBonus,
            AllowedSlots = WeaponSlots,
            Tags = [AffixTag.Critical, AffixTag.Damage],
            TierTable =
            [
                new PercentTierRange(1, 4, 6),
                new PercentTierRange(2, 7, 9),
                new PercentTierRange(3, 10, 13),
                new PercentTierRange(4, 14, 18),
                new PercentTierRange(5, 19, 25)
            ]
        },
        [StatId.SentinelCooldownRecoveryRate] = new Affix
        {
            Name = "Celerity",
            DescriptionTemplate = "+{0}% Ability Cooldown Recovery Rate",
            AffixType = AffixType.Prefix,
            Stat = StatId.SentinelCooldownRecoveryRate,
            AllowedSlots = ArmorSlots,
            Tags = [AffixTag.Cooldown],
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
            Tags = [AffixTag.Damage],
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
    public static IEnumerable<AffixTierEntry> GetTierEntries(Item item, AffixType type, int bestTier)
    {
        return _affixes.Values
            .Where(affix => affix.AffixType == type)
            .Where(affix => affix.AllowedSlots.Length == 0 || affix.AllowedSlots.Contains(item.Slot))
            .Where(affix => affix.IsAvailableTo(item.EquippableCharacter))
            .Where(affix => IsEligibleForBaseType(affix, item))
            .SelectMany(affix => affix.TierTable
                .Where(tier => tier.Tier <= bestTier)
                .Select(tier => new AffixTierEntry(affix, tier)));
    }

    // A defense mod (Armor/Shield/the Hybrid combo) is only eligible if it matches the item's own
    // rolled BaseType - a Hybrid piece can only roll SentinelArmorAndShield, a pure Armor piece
    // can only roll SentinelArmor, and so on. Every other mod is unaffected.
    private static bool IsEligibleForBaseType(Affix affix, Item item)
    {
        if (item is not Armor armor)
        {
            return true;
        }

        return affix.Stat switch
        {
            StatId.SentinelArmor => armor.BaseType == BaseType.Armor,
            StatId.SentinelShield => armor.BaseType == BaseType.Shield,
            StatId.SentinelArmorAndShield => armor.BaseType == BaseType.Hybrid,
            _ => true
        };
    }
}
