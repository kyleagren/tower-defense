using Godot;
using System.Collections.Generic;

// The single canonical rarity -> color mapping, shared by anything that visually represents an
// item's rarity (InventoryPanel's tile backgrounds, WorldItem's pickup sphere).
public static class RarityColors
{
    private static readonly Dictionary<Rarity, Color> _colors = new()
    {
        [Rarity.Common] = new Color(0.24f, 0.24f, 0.24f),
        [Rarity.Uncommon] = new Color(0.14f, 0.30f, 0.14f),
        [Rarity.Rare] = new Color(0.14f, 0.20f, 0.36f),
        [Rarity.Legendary] = new Color(0.36f, 0.22f, 0.08f),
        [Rarity.Mythic] = new Color(0.32f, 0.10f, 0.32f),
        [Rarity.Unique] = new Color(0.40f, 0.32f, 0.08f)
    };

    public static Color Get(Rarity rarity)
    {
        return _colors.TryGetValue(rarity, out Color color) ? color : _colors[Rarity.Common];
    }
}
