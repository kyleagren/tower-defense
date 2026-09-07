using System.Collections.Generic;

// What each BaseType grants and which icon suffix it uses - hand-authored, small table, mirroring
// AffixTable's shape. A future 4th/5th base type is a new table row, not a new C# class.
public static class ArmorBaseTypeTable
{
    private static readonly Dictionary<BaseType, StatId[]> _grantedStats = new()
    {
        [BaseType.Hybrid] = new[] { StatId.SentinelArmor, StatId.SentinelShield },
        [BaseType.Armor] = new[] { StatId.SentinelArmor },
        [BaseType.Shield] = new[] { StatId.SentinelShield }
    };

    private static readonly Dictionary<BaseType, string> _iconSuffixes = new()
    {
        [BaseType.Hybrid] = "",
        [BaseType.Armor] = "_armor",
        [BaseType.Shield] = "_shield"
    };

    private static readonly BaseType[] _all = { BaseType.Hybrid, BaseType.Armor, BaseType.Shield };

    public static StatId[] GetGrantedStats(BaseType baseType) => _grantedStats[baseType];
    public static string GetIconSuffix(BaseType baseType) => _iconSuffixes[baseType];
    public static BaseType Random(System.Random random) => _all[random.Next(_all.Length)];
}
