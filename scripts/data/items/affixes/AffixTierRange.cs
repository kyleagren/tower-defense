public abstract class AffixTierRange
{
    public readonly int Tier;
    public readonly float MinValue;
    public readonly float MaxValue;

    protected AffixTierRange(int tier, float minValue, float maxValue)
    {
        Tier = tier;
        MinValue = minValue;
        MaxValue = maxValue;
    }
}
