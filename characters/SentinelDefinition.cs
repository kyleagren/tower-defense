using Godot;

// A Sentinel's base values - what its stats start from before gear. A Resource rather than exports
// on the Sentinel's scene, so the stat math can run with no Sentinel in the level: the character
// sheet reads it in Build, while the Architect has taken over.
[GlobalClass]
public partial class SentinelDefinition : Resource
{
    [Export] public float BaseMaxHealth { get; private set; } = 100f;

    // A Sentinel's own swing on its armor, on top of gear - the Tank's heat dial.
    public virtual float ArmorMultiplier(PlayerLevelState state) => 1f;
}
