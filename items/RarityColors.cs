using Godot;
using System.Collections.Generic;

// The single canonical rarity -> color mapping, shared by anything that visually represents an
// item's rarity (InventoryPanel's tile backgrounds, WorldItem's pickup sphere), and the text
// variant for rarity-colored labels.
public static class RarityColors
{
    private static readonly Dictionary<Rarity, Color> _colors = new()
    {
        [Rarity.Common] = new Color(0.28f, 0.28f, 0.28f),
        [Rarity.Uncommon] = new Color(0.08f, 0.30f, 0.06f),
        [Rarity.Rare] = new Color(0.06f, 0.24f, 0.38f),
        [Rarity.Epic] = new Color(0.30f, 0.10f, 0.42f),
        [Rarity.Legendary] = new Color(0.40f, 0.38f, 0.05f),
        [Rarity.Unique] = new Color(0.42f, 0.14f, 0.02f)
    };

    // Bright variants for rarity-colored text (item nameplates) - the dark tones above are for
    // backgrounds and would be unreadable as a label on a dark plate.
    private static readonly Dictionary<Rarity, Color> _textColors = new()
    {
        [Rarity.Common] = new Color(0.85f, 0.85f, 0.85f),
        [Rarity.Uncommon] = new Color(0.35f, 0.95f, 0.30f),
        [Rarity.Rare] = new Color(0.35f, 0.75f, 1f),
        [Rarity.Epic] = new Color(0.85f, 0.40f, 1f),
        [Rarity.Legendary] = new Color(1f, 0.85f, 0.25f),
        [Rarity.Unique] = new Color(1f, 0.45f, 0.15f)
    };

    public static Color Get(Rarity rarity)
    {
        return _colors.TryGetValue(rarity, out Color color) ? color : _colors[Rarity.Common];
    }

    public static Color GetText(Rarity rarity)
    {
        return _textColors.TryGetValue(rarity, out Color color) ? color : _textColors[Rarity.Common];
    }
}
