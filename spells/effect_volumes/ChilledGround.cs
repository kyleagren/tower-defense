using Godot;

public partial class ChilledGround : GroundEffect
{
    [Export] public float SlowPercent { get; private set; } = 0.25f;

    protected override void OnTick(Enemy enemy)
    {
        enemy.ApplyAilment(new ChilledGroundAilment(SlowPercent, EffectDuration));
    }
}
