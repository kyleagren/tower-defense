public readonly struct AffixTierRange
{
    public readonly int Tier;
    public readonly float MinValue;
    public readonly float MaxValue;

    public AffixTierRange(int tier, float minValue, float maxValue)
    {
        Tier = tier;
        MinValue = minValue;
        MaxValue = maxValue;
    }
}
