using Godot;

[GlobalClass]
public partial class MostEnemiesNearbyTargetingRune : TargetingRune
{
    public override string Name => "Swarm";

    protected override TargetPriority Priority => TargetPriority.MostEnemiesNearby;

    public override string Description => "This ritual targets the enemy with the most others around it.";
}
