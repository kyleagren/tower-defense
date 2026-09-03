public sealed class FlatTierRange : AffixTierRange
{
    public FlatTierRange(int tier, float minValue, float maxValue, int weight = 1000)
        : base(tier, minValue, maxValue, weight)
    {
    }
}
