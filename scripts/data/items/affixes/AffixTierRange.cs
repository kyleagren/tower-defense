using Godot;

public abstract class AffixTierRange
{
    // 1 = weakest, ascending = stronger (deliberately the reverse of PoE's own T1-is-best
    // convention) - a future stronger tier is always just a new highest number (6, 7, ...), so
    // existing tiers never need renumbering to make room for it.
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

    // Shared by LootGenerator's initial roll and the crafting table's add/reroll actions, so there's
    // one "how a value lands within a tier's range" rule instead of two copies drifting apart.
    public float RollValue(System.Random random)
    {
        float value = MinValue + (float)random.NextDouble() * (MaxValue - MinValue);
        return Mathf.Round(value * 100f) / 100f;
    }
}
