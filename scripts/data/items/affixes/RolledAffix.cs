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
        return string.Format(Affix.DescriptionTemplate, Mathf.RoundToInt(Value * 100f));
    }

    // The advanced line - description plus which tier it rolled and the range that tier could
    // have landed in. Shared by the crafting table's bars and the advanced (Alt-hover) tooltip,
    // so there's one "how a rolled mod reads with its tier shown" format, not two copies.
    public string BuildAdvancedDescription()
    {
        AffixTierRange range = TierRange;
        int minPercent = Mathf.RoundToInt(range.MinValue * 100f);
        int maxPercent = Mathf.RoundToInt(range.MaxValue * 100f);

        return $"{Affix.Name} (T{Tier})  {BuildDescription()}  [{minPercent}-{maxPercent}%]";
    }
}
