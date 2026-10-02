using Godot;

[GlobalClass]
public partial class LowestHealthTargetingRune : TargetingRune
{
    public override string Name => "Cull";

    protected override TargetPriority Priority => TargetPriority.LowestHealth;

    public override string Description => "This ritual targets the enemy with the least health.";
}
