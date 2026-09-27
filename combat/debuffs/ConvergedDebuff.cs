// Converge's root: the host can't walk anywhere, though it can still turn and attack. Short-lived and
// refreshed by the field every tick, so it ends on its own once the enemy is out of the field.
public class ConvergedDebuff : Debuff
{
    private float _remainingDuration;

    public ConvergedDebuff(float duration)
    {
        _remainingDuration = duration;
    }

    public override DebuffType Type => DebuffType.Converged;

    public override float SpeedMultiplier => 0f;

    public override Godot.Collections.Array ToNetwork() => new() { (int)Type, _remainingDuration };

    public static ConvergedDebuff Decode(Godot.Collections.Array data) => new(data[1].AsSingle());

    public override bool Tick(Enemy enemy, float delta)
    {
        _remainingDuration -= delta;
        return _remainingDuration > 0f;
    }
}
