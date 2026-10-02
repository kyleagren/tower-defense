using Godot;

[GlobalClass]
public partial class HighestMaxHealthTargetingRune : TargetingRune
{
    public override string Name => "Giantslayer";

    protected override TargetPriority Priority => TargetPriority.HighestMaxHealth;

    protected override string TargetingDescription => "This ritual targets the toughest enemy in range.";
}
