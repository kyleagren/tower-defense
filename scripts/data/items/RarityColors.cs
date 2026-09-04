using Godot;
using System.Collections.Generic;

// The single canonical rarity -> color mapping, shared by anything that visually represents an
// item's rarity (InventoryPanel's tile backgrounds, WorldItem's pickup sphere).
public static class RarityColors
{
    private static readonly Dictionary<Rarity, Color> _colors = new()
    {
        [Rarity.Common] = new Color(0.28f, 0.28f, 0.28f),
        [Rarity.Uncommon] = new Color(0.08f, 0.30f, 0.06f),
        [Rarity.Rare] = new Color(0.06f, 0.24f, 0.38f),
        [Rarity.Epic] = new Color(0.30f, 0.10f, 0.42f),
        [Rarity.Mythic] = new Color(0.40f, 0.38f, 0.05f),
        [Rarity.Unique] = new Color(0.42f, 0.14f, 0.02f)
    };

    public static Color Get(Rarity rarity)
    {
        return _colors.TryGetValue(rarity, out Color color) ? color : _colors[Rarity.Common];
    }
}
