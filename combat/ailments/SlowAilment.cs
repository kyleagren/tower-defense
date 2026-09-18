using Godot;

// Shared by every ailment whose whole effect is a movement slow. Distinct subclasses rather than one
// parameterized class because slotting keys on type: two different sources of slow are meant to
// stack multiplicatively, while two of the same source compete strongest-wins.
public abstract class SlowAilment : Ailment
{
    private readonly float _slowPercent;
    private float _remainingDuration;

    protected SlowAilment(float slowPercent, float duration)
    {
        _slowPercent = Mathf.Clamp(slowPercent, 0f, 1f);
        _remainingDuration = duration;
    }

    public override float SpeedMultiplier => 1f - _slowPercent;

    protected override float Magnitude => _slowPercent;

    public override bool Tick(Enemy enemy, float delta)
    {
        _remainingDuration -= delta;
        return _remainingDuration > 0f;
    }
}
