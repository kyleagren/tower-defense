using System.Linq;
using Godot;

// The small, per-item record of one actual roll - which stat, which tier, what value landed.
// Everything else about the mod (name, description, slot rules) is looked up through Affix
// rather than duplicated here.
[GlobalClass]
public partial class RolledAffix : Resource
{
    [Export] public StatId Stat { get; set; }
    [Export] public int Tier { get; set; }
    [Export] public float Value { get; set; }

    public Affix Affix => AffixTable.Get(Stat);

    // Looked up rather than stored - only the crafting bars and Reroll need the full range, and
    // re-deriving it avoids a second copy of Min/Max drifting from AffixTable's own numbers.
    public AffixTierRange TierRange => Affix.TierTable.First(tier => tier.Tier == Tier);

    // The plain description line ("+7% Move Speed") - the mod's own template filled in with the
    // rolled value. Every current affix is percentage-based (Value is stored as a 0-1 fraction) -
    // a future flat-value affix would need a different formatting path here.
    public string BuildDescription()
    {
        object formattedValue = Affix.IsFlatValue ? Mathf.RoundToInt(Value) : Mathf.RoundToInt(Value * 100f);
        return string.Format(Affix.ResolvedTemplate, formattedValue);
    }

    // PoE-style inline value+range ("+35(33-38)% Increased Energy Shield") - the rolled value
    // immediately followed by its possible range in parens, substituted as one unit into the
    // mod's own template. Shared by the crafting table's bars and ItemTooltip's advanced rows,
    // both of which show tier separately (a bar's own text, or a row's tier badge).
    public string BuildInlineRangeDescription()
    {
        AffixTierRange range = TierRange;

        if (Affix.IsFlatValue)
        {
            int value = Mathf.RoundToInt(Value);
            int minValue = Mathf.RoundToInt(range.MinValue);
            int maxValue = Mathf.RoundToInt(range.MaxValue);
            return string.Format(Affix.ResolvedTemplate, $"{value}({minValue}-{maxValue})");
        }

        int percentValue = Mathf.RoundToInt(Value * 100f);
        int minPercent = Mathf.RoundToInt(range.MinValue * 100f);
        int maxPercent = Mathf.RoundToInt(range.MaxValue * 100f);

        return string.Format(Affix.ResolvedTemplate, $"{percentValue}({minPercent}-{maxPercent})");
    }

    // The nested advanced-tooltip popup's header - which type of mod it is, its name, and tier.
    // Its tag list is built separately by ItemTooltip, which renders "Tags" in a different color
    // than the tags themselves.
    public string BuildModHeaderText()
    {
        return $"{Affix.AffixType} Modifier \"{Affix.Name}\" (Tier: {Tier})";
    }
}
