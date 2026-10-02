using Godot;

[GlobalClass]
public partial class FurthestEnemyTargetingRune : TargetingRune
{
    public override string Name => "Horizon";

    protected override TargetPriority Priority => TargetPriority.FurthestFromRitual;

    protected override string TargetingDescription => "This ritual targets the enemy furthest from it, still in range.";
}
