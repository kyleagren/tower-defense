public abstract class AffixTierRange
{
    public readonly int Tier;
    public readonly float MinValue;
    public readonly float MaxValue;
    public readonly int Weight;

    protected AffixTierRange(int tier, float minValue, float maxValue, int weight)
    {
        Tier = tier;
        MinValue = minValue;
        MaxValue = maxValue;
        Weight = weight;
    }
}
