using Godot;

[GlobalClass]
public partial class HighestMaxHealthTargetingRune : TargetingRune
{
    public override string Name => "Giantslayer";

    protected override TargetPriority Priority => TargetPriority.HighestMaxHealth;

    public override string Description => "This ritual targets the toughest enemy in range.";
}
