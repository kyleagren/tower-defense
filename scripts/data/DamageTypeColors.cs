using Godot;
using System.Collections.Generic;

// The single canonical element -> color mapping, shared by floating combat text and by Enemy's
// elemental status overlay. Two accessors rather than two tables: the two consumers agree on every
// element except Water, where a dark navy tints a lit mesh fine but reads poorly as flat text. That
// one divergence is an explicit override here instead of a reason to maintain a second copy of the
// whole palette somewhere else.
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

    private static readonly Dictionary<Tag, Color> _statusOverlayOverrides = new()
    {
        [Tag.Water] = new Color(0.05f, 0.15f, 0.55f)
    };

    // Physical (and, rarely, pure Wind - DominantElement doesn't candidate it either) both fall
    // back to the neutral physical color, matching WoW's own convention of white physical numbers.
    public static Color Get(Tag? element)
    {
        return element.HasValue && _elementColors.TryGetValue(element.Value, out Color color) ? color : Physical;
    }

    // The tint for an enemy currently carrying this element as its Status, applied as an unshaded
    // mesh overlay rather than drawn as text.
    public static Color GetStatusOverlay(Tag element)
    {
        return _statusOverlayOverrides.TryGetValue(element, out Color color) ? color : Get(element);
    }
}
