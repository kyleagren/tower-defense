using Godot;

// The small, per-item record of one rolled base stat - which stat, what value. No tier; this
// isn't drawn from AffixTable, it's derived from Rarity + BaseType at generation time.
[GlobalClass]
public partial class RolledBaseStat : Resource
{
    [Export] public StatId Stat { get; set; }
    [Export] public float Value { get; set; }
}
