using Godot;

// A Sentinel's resolved stats - its base values, its owner's gear and its level state - worked out
// without needing a body. The Sentinel reads its own stats through this too, so the character
// sheet and the Sentinel can never disagree.
public readonly struct SentinelStats
{
    private readonly SentinelDefinition _definition;
    private readonly CharacterSheet _sheet;
    private readonly PlayerLevelState _state;

    public SentinelStats(SentinelDefinition definition, CharacterSheet sheet, PlayerLevelState state)
    {
        _definition = definition;
        _sheet = sheet;
        _state = state;
    }

    // The player's selected Sentinel, whether or not one is in the level right now.
    public static SentinelStats For(PlayerId player)
    {
        PlayerProfile profile = PlayerProfiles.Get(player);
        return new SentinelStats(
            SentinelCatalog.Definition(profile.SelectedCharacter),
            profile.SelectedSheet,
            LevelContext.Current.StateOf(player));
    }

    public float MaxHealth => _definition.BaseMaxHealth * (1f + _sheet.GetBonus(StatId.SentinelMaxHealth));

    public float MaxShield =>
        _sheet.GetLocalTotal(StatId.SentinelShield) * (1f + _sheet.GetBonus(StatId.SentinelTotalShield));

    public float TotalArmor =>
        _sheet.GetLocalTotal(StatId.SentinelArmor) * (1f + _sheet.GetBonus(StatId.SentinelTotalArmor)) * _definition.ArmorMultiplier(_state);

    // Armor's reduction against melee; projectiles get GameRules.ArmorProjectileEffectiveness of it.
    // S-curve (exponent > 1): trivial gear barely registers, endgame gear feels strong, climbs
    // faster in the middle than at either end.
    public float MeleeDamageReductionPercent
    {
        get
        {
            float armorPow = Mathf.Pow(TotalArmor, GameRules.SentinelArmorCurveExponent);
            float kPow = Mathf.Pow(GameRules.SentinelArmorCurveConstant, GameRules.SentinelArmorCurveExponent);
            return armorPow / (armorPow + kPow);
        }
    }

    public float BaseHealthRegenPerSecond => _sheet.GetBonus(StatId.SentinelBaseHealthRegen);

    public float TotalHealthRegenPerSecond =>
        BaseHealthRegenPerSecond * (1f + _sheet.GetBonus(StatId.SentinelHealthRegen));
}
