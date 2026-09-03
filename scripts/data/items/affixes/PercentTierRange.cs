// minPercent/maxPercent are whole percentage points (5 means 5%) so the mod pool reads
// naturally - stored as the plain 0-1 fraction gameplay code actually multiplies by.
public sealed class PercentTierRange : AffixTierRange
{
    public PercentTierRange(int tier, float minPercent, float maxPercent)
        : base(tier, minPercent / 100f, maxPercent / 100f)
    {
    }
}
