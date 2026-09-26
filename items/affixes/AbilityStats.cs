public enum AbilityStatKind
{
    CooldownRecoveryRate,
    Area,
    Duration,
    Damage
}

// The ability stats as tables rather than arithmetic on StatId's values, so nothing leans on the
// enum's numeric order beyond it being append-only.
public static class AbilityStats
{
    public const int SlotCount = 4;

    private static readonly StatId[,] _perSlot =
    {
        { StatId.Ability1CooldownRecoveryRate, StatId.Ability1Area, StatId.Ability1Duration, StatId.Ability1Damage },
        { StatId.Ability2CooldownRecoveryRate, StatId.Ability2Area, StatId.Ability2Duration, StatId.Ability2Damage },
        { StatId.Ability3CooldownRecoveryRate, StatId.Ability3Area, StatId.Ability3Duration, StatId.Ability3Damage },
        { StatId.Ability4CooldownRecoveryRate, StatId.Ability4Area, StatId.Ability4Duration, StatId.Ability4Damage }
    };

    private static readonly StatId[] _generic =
    {
        StatId.SentinelCooldownRecoveryRate,
        StatId.AbilityArea,
        StatId.AbilityDuration,
        StatId.AbilityDamage
    };

    public static StatId For(int slot, AbilityStatKind kind) => _perSlot[slot, (int)kind];

    public static StatId Generic(AbilityStatKind kind) => _generic[(int)kind];
}
