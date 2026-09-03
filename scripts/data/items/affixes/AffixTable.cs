using System.Collections.Generic;

// The full mod pool, hardcoded rather than authored as assets - there are only a few dozen of
// these at most, they're never touched by anything but us, and having them all in one file beats
// clicking through two dozen .tres files to see the whole pool at once.
public static class AffixTable
{
    private static readonly Dictionary<StatId, Affix> _affixes = new()
    {
        [StatId.SentinelMoveSpeed] = new Affix
        {
            Name = "Swift",
            DescriptionTemplate = "+{0}% Move Speed",
            AffixType = AffixType.Suffix,
            Stat = StatId.SentinelMoveSpeed,
            TierTable = new[]
            {
                new AffixTierRange(5, 0.03f, 0.05f),
                new AffixTierRange(4, 0.06f, 0.08f),
                new AffixTierRange(3, 0.09f, 0.11f),
                new AffixTierRange(2, 0.12f, 0.15f),
                new AffixTierRange(1, 0.16f, 0.20f)
            }
        }
    };

    public static Affix Get(StatId stat)
    {
        return _affixes[stat];
    }
}
