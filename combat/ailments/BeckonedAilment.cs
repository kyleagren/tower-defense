// Beckon's root: the host can't walk anywhere, though it can still turn and attack. Short-lived and
// refreshed by the field every tick, so it ends on its own once the enemy is out of the field.
public class BeckonedAilment : Ailment
{
    private float _remainingDuration;

    public BeckonedAilment(float duration)
    {
        _remainingDuration = duration;
    }

    public override float SpeedMultiplier => 0f;

    public override bool Tick(Enemy enemy, float delta)
    {
        _remainingDuration -= delta;
        return _remainingDuration > 0f;
    }
}
