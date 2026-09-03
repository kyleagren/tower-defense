// minPercent/maxPercent are whole percentage points (5 means 5%) so the mod pool reads
// naturally - stored as the plain 0-1 fraction gameplay code actually multiplies by. Weight
// defaults to an even 1000 across a mod's tiers; override it per-case to make a specific tier
// (usually a top one) rarer without touching how often the mod as a whole shows up.
public sealed class PercentTierRange : AffixTierRange
{
    public PercentTierRange(int tier, float minPercent, float maxPercent, int weight = 1000)
        : base(tier, minPercent / 100f, maxPercent / 100f, weight)
    {
    }
}
