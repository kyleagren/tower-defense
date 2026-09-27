using Godot;

// The Tank's base values, including what its heat dial does to its stats: Fire added to each swing
// above equilibrium, armor shifted either way.
[GlobalClass]
public partial class TankDefinition : SentinelDefinition
{
    [Export] public float MaxHeatFireFraction { get; private set; } = 0.2f;
    [Export] public float MaxHeatArmorShift { get; private set; } = 0.2f;

    // One formula for both sides: positive heat lowers armor, negative heat raises it.
    public override float ArmorMultiplier(PlayerLevelState state) => 1f - MaxHeatArmorShift * state.Heat / Tank.HeatLimit;
}
