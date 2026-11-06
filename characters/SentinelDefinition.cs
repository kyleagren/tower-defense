using Godot;

// A Sentinel's base values - what its stats start from before gear. A Resource rather than exports
// on the Sentinel's scene, so the stat math can run with no Sentinel of that kind in the level.
[GlobalClass]
public partial class SentinelDefinition : Resource
{
    [Export] public float BaseMaxHealth { get; private set; } = 100f;

    // A Sentinel's own swing on its armor, on top of gear - the Tank's heat dial.
    public virtual float ArmorMultiplier(PlayerLevelState state) => 1f;
}
