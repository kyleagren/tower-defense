using Godot;

[GlobalClass]
public partial class ClosestEnemyTargetingRune : TargetingRune
{
    public override string Name => "Proximity";

    protected override TargetPriority Priority => TargetPriority.ClosestToRitual;

    protected override string TargetingDescription => "This ritual targets the enemy closest to it.";
}
