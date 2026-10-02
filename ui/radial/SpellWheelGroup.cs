using Godot;
using System.Collections.Generic;

// The wedges of the spell wheel's element ring, in ring order clockwise.
public enum SpellWheelGroup
{
    Fire,
    Lightning,
    Cold,
    Water,
    Wind,
    Physical,
    MultiElement
}

public static class SpellWheelGroups
{
    private static readonly Dictionary<Tag, SpellWheelGroup> _groupByElement = new()
    {
        [Tag.Fire] = SpellWheelGroup.Fire,
        [Tag.Lightning] = SpellWheelGroup.Lightning,
        [Tag.Cold] = SpellWheelGroup.Cold,
        [Tag.Water] = SpellWheelGroup.Water,
        [Tag.Wind] = SpellWheelGroup.Wind
    };

    // Placeholder until a wedge can draw a real rainbow.
    private static readonly Color _multiElementColor = new Color(1f, 0.85f, 0.2f);

    // Worked out from tags: one element tag puts a spell under that element, none under Physical,
    // and more than one under MultiElement.
    public static SpellWheelGroup Of(Spell spell)
    {
        SpellWheelGroup? found = null;

        foreach (Tag tag in spell.Tags)
        {
            if (!_groupByElement.TryGetValue(tag, out SpellWheelGroup group))
            {
                continue;
            }

            if (found.HasValue)
            {
                return SpellWheelGroup.MultiElement;
            }

            found = group;
        }

        return found ?? SpellWheelGroup.Physical;
    }

    public static Color ColorOf(SpellWheelGroup group) => group switch
    {
        SpellWheelGroup.Fire => DamageTypeColors.Get(Tag.Fire),
        SpellWheelGroup.Lightning => DamageTypeColors.Get(Tag.Lightning),
        SpellWheelGroup.Cold => DamageTypeColors.Get(Tag.Cold),
        SpellWheelGroup.Water => DamageTypeColors.Get(Tag.Water),
        SpellWheelGroup.Wind => DamageTypeColors.Get(Tag.Wind),
        SpellWheelGroup.MultiElement => _multiElementColor,
        _ => DamageTypeColors.Get(null)
    };

    public static string NameOf(SpellWheelGroup group) =>
        group == SpellWheelGroup.MultiElement ? "Multi-Element" : group.ToString();
}
