// A tier range whose values are the raw number itself, not a percentage - e.g. "+3 Health
// Regenerated per Second" rather than "+3%". Mirrors PercentTierRange's shape exactly, just
// without the /100 conversion, since AffixTierRange.RollValue() already works for either.
public sealed class FlatTierRange : AffixTierRange
{
    public FlatTierRange(int tier, float minValue, float maxValue, int weight = 1000)
        : base(tier, minValue, maxValue, weight)
    {
    }
}
