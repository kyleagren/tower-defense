using System.Collections.Generic;

// Total base value (before BaseType splits it) a piece of Armor grants, scaled by Rarity.
public static class ArmorBaseStats
{
    private static readonly Dictionary<Rarity, float> _singleSlotTotal = new()
    {
        [Rarity.Common] = 100f,
        [Rarity.Uncommon] = 200f,
        [Rarity.Rare] = 300f,
        [Rarity.Epic] = 400f,
        [Rarity.Legendary] = 500f
    };

    public static float GetTotal(Rarity rarity, float slotWeight) =>
        _singleSlotTotal.TryGetValue(rarity, out float value) ? value * slotWeight : 0f;
}
