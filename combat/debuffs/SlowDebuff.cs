using Godot;

// Shared by every debuff whose whole effect is a movement slow - elemental or not. Distinct
// subclasses rather than one parameterized class because slotting keys on type: two different
// sources of slow are meant to stack multiplicatively, while two of the same source compete
// strongest-wins.
public abstract class SlowDebuff : Debuff
{
    private readonly float _slowPercent;
    private float _remainingDuration;

    protected SlowDebuff(float slowPercent, float duration)
    {
        _slowPercent = Mathf.Clamp(slowPercent, 0f, 1f);
        _remainingDuration = duration;
    }

    protected float SlowPercent => _slowPercent;
    protected float RemainingDuration => _remainingDuration;

    public override float SpeedMultiplier => 1f - _slowPercent;

    protected override float Magnitude => _slowPercent;

    public override bool Tick(Enemy enemy, float delta)
    {
        _remainingDuration -= delta;
        return _remainingDuration > 0f;
    }
}
