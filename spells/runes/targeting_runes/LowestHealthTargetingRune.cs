using Godot;

[GlobalClass]
public partial class LowestHealthTargetingRune : TargetingRune
{
    public override string Name => "Cull";

    protected override TargetPriority Priority => TargetPriority.LowestHealth;

    protected override string TargetingDescription => "This ritual targets the enemy with the least health.";
}
