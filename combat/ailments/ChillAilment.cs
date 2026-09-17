using Godot;

// Slows its host's movement for a fixed duration. Applied by ChillRune off any Cold damage.
public class ChillAilment : Ailment
{
    private readonly float _slowPercent;
    private float _remainingDuration;

    public ChillAilment(float slowPercent, float duration)
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
