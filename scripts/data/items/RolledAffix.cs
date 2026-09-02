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
}
