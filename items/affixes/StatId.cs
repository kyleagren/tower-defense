public enum StatId
{
    RitualRange,
    RitualCastSpeed,
    RitualCooldownRecoveryRate,
    RitualManaCostReduction,
    RitualCapacityCostReduction,
    RitualDamage,
    RitualFireDamage,
    RitualWaterDamage,
    RitualColdDamage,
    RitualLightningDamage,
    RitualWindDamage,

    SentinelMaxHealth,
    SentinelMoveSpeed,
    SentinelArmor,
    SentinelShield,
    SentinelArmorAndShield,
    SentinelTotalArmor,
    SentinelTotalShield,
    SentinelBaseHealthRegen,
    SentinelHealthRegen,
    SentinelCritChance,
    SentinelBaseCritChance,
    SentinelIncreasedAttackSpeed,
    SentinelCritDamage,
    SentinelMoreCritDamageBonus,
    SentinelCooldownRecoveryRate,

    WeaponAttackDamage,

    // Every stat below is appended rather than grouped with its kin, because saves store StatId as a
    // number - inserting one earlier would re-map every mod already rolled.

    // Generic ability stats, applying to every slot. SentinelCooldownRecoveryRate is the cooldown one.
    AbilityArea,
    AbilityDuration,
    AbilityDamage,

    // Per ability slot, four kinds each - see AbilityStats.
    Ability1CooldownRecoveryRate,
    Ability1Area,
    Ability1Duration,
    Ability1Damage,
    Ability2CooldownRecoveryRate,
    Ability2Area,
    Ability2Duration,
    Ability2Damage,
    Ability3CooldownRecoveryRate,
    Ability3Area,
    Ability3Duration,
    Ability3Damage,
    Ability4CooldownRecoveryRate,
    Ability4Area,
    Ability4Duration,
    Ability4Damage,

    RitualArea,
    RitualDuration
}
