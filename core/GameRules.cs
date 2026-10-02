public static class GameRules
{
    public const int MaxRuneSlots = 4;
    public const int MaxTokens = 3;
    public const int PinnedFavoriteSlots = 8;
    public const float RitualReach = 4f;
    public const float ItemPickupReach = 3f;
    // How far a character can walk from a station before its panel closes - past ItemPickupReach,
    // so stepping back from the counter doesn't slam it shut.
    public const float StationLeaveReach = 5f;
    public const int MaxPrefixAffixes = 3;
    public const int MaxSuffixAffixes = 3;
    public const int CraftingActionGoldCost = 1;
    public const int StashTabGoldCost = 10;
    public const int VendorRerollGoldCost = 1;
    public const float SentinelArmorCurveConstant = 1650f;
    public const float SentinelArmorCurveExponent = 1.3f;
    public const float ShieldRegenDelay = 3.5f;
    public const float ShieldValuePerArmorPoint = 0.25f;
    public const float ShieldProjectileEffectiveness = 2f;
    public const float ArmorProjectileEffectiveness = 0.5f;
    public const float ShieldRegenRatePercent = 0.10f;
    public const float OvershieldCeilingMultiplier = 2f;
    public const float AbilityHoldThreshold = 0.3f;
    public const float RepairCostFraction = 0.5f;
    public const float UnarmedCritChance = 0.05f;
    public const float UnarmedCritDamageBonus = 0.5f;
    public const float UnarmedAttackDamage = 5f;
    public const float UnarmedAttackSpeed = 1f;
    public const float ReadyCountdownSeconds = 90f;
    public const float RespawnBaseSeconds = 5f;
    public const float RespawnSecondsPerExtraDeath = 5f;
    public const int RespawnSkipBaseManaCost = 10;
    public const int RespawnSkipManaCostPerExtraDeath = 5;
    // Each player past the first adds this much to an enemy's health: 150% for two, up to 250% for four.
    public const float EnemyHealthPerExtraPlayer = 0.5f;
    // Swirl's burst: how far it reaches, and its damage as a share of the triggering hit's Wind.
    public const float SwirlRadius = 3f;
    public const float SwirlDamageFraction = 0.3f;
}
