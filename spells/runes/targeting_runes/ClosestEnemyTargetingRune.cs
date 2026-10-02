using Godot;

[GlobalClass]
public partial class ClosestEnemyTargetingRune : TargetingRune
{
    public override string Name => "Proximity";

    protected override TargetPriority Priority => TargetPriority.ClosestToRitual;

    public override string Description => "This ritual targets the enemy closest to it.";
}
