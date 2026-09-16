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
    private static readonly Color TrueDamage = Colors.Black;
    private static readonly Color DefaultOutline = Colors.Black;
    private static readonly Color TrueDamageOutline = Colors.White;

    private static readonly Dictionary<Tag, Color> _elementColors = new()
    {
        [Tag.Fire] = new Color(1f, 0.5f, 0f),
        [Tag.Water] = new Color(0.3f, 0.55f, 1f),
        [Tag.Cold] = new Color(0.6f, 0.85f, 1f),
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

    // Fill and outline resolve together because true damage inverts the pair: every other number is
    // color-on-black, so black-on-white is the one combination that can't be mistaken for an
    // element. Picking either half alone would render it invisible against the other.
    public static (Color Fill, Color Outline) GetForDamage(DamagePacket damage)
    {
        return damage.IsTrueDamage
            ? (TrueDamage, TrueDamageOutline)
            : (Get(damage.DominantElement), DefaultOutline);
    }

    // A spell's own dominant damage element misses anything that deals no direct damage at all -
    // Ice Wall carries Tag.Cold for identity and build-crafting but has no FireDamage/ColdDamage of
    // its own to be dominant, so Get(spell.BaseDamage.DominantElement) resolves it to the neutral
    // white/grey. Falling back to the spell's own Tags list catches that (and any future
    // damage-less elemental spell) without special-casing Ice Wall.
    public static Color GetForSpell(Spell spell)
    {
        Tag? dominant = spell.BaseDamage.DominantElement;

        if (dominant.HasValue && _elementColors.TryGetValue(dominant.Value, out Color dominantColor))
        {
            return dominantColor;
        }

        foreach (Tag tag in spell.Tags)
        {
            if (_elementColors.TryGetValue(tag, out Color tagColor))
            {
                return tagColor;
            }
        }

        return Physical;
    }

    // The tint for an enemy currently carrying this element as its Status, applied as an unshaded
    // mesh overlay rather than drawn as text.
    public static Color GetStatusOverlay(Tag element)
    {
        return _statusOverlayOverrides.TryGetValue(element, out Color color) ? color : Get(element);
    }
}
