using Godot;
using System.Collections.Generic;

// Damage-type -> color for floating combat text. Reuses the same Fire/Cold/Lightning hues Enemy's
// own elemental status-flash overlay already uses (Water is brightened here specifically for text
// legibility - the status overlay's dark navy tints a lit mesh fine, but reads poorly as flat text).
public static class DamageTypeColors
{
    private static readonly Color Physical = new Color(0.9f, 0.9f, 0.9f);

    private static readonly Dictionary<Tag, Color> _elementColors = new()
    {
        [Tag.Fire] = new Color(1f, 0.5f, 0f),
        [Tag.Water] = new Color(0.3f, 0.55f, 1f),
        [Tag.Cold] = new Color(0.75f, 0.95f, 1f),
        [Tag.Lightning] = new Color(1f, 0.9f, 0.1f),
        [Tag.Wind] = new Color(0.75f, 1f, 0.85f)
    };

    // Physical (and, rarely, pure Wind - DominantElement doesn't candidate it either) both fall
    // back to the neutral physical color, matching WoW's own convention of white physical numbers.
    public static Color Get(Tag? element)
    {
        return element.HasValue && _elementColors.TryGetValue(element.Value, out Color color) ? color : Physical;
    }
}
