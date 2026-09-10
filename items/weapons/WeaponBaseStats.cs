using System.Collections.Generic;

// Rarity-scaled base attack damage per weapon type, mirroring ArmorBaseStats' shape - each weapon
// type gets its own starting value and per-tier increment rather than sharing one table, since Gun
// and SwordAndShield have genuinely different numbers, not just a shared slot-weight multiplier.
public static class WeaponBaseStats
{
    private static readonly Dictionary<Rarity, float> _gunDamage = BuildTable(25f, 10f);
    private static readonly Dictionary<Rarity, float> _swordDamage = BuildTable(50f, 15f);

    public static float GetGunDamage(Rarity rarity) => _gunDamage.GetValueOrDefault(rarity, 0f);
    public static float GetSwordDamage(Rarity rarity) => _swordDamage.GetValueOrDefault(rarity, 0f);

    private static Dictionary<Rarity, float> BuildTable(float commonValue, float perRarityTier)
    {
        return new Dictionary<Rarity, float>
        {
            [Rarity.Common] = commonValue,
            [Rarity.Uncommon] = commonValue + perRarityTier,
            [Rarity.Rare] = commonValue + perRarityTier * 2f,
            [Rarity.Epic] = commonValue + perRarityTier * 3f,
            [Rarity.Legendary] = commonValue + perRarityTier * 4f
        };
    }
}
