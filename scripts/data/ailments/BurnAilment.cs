public class BurnAilment : Ailment
{
    private const float TickInterval = 1f;

    private float _damagePerSecond;
    private float _remainingDuration;
    private float _timeSinceLastTick;

    public BurnAilment(float totalDamage, float duration)
    {
        _remainingDuration = duration;
        _damagePerSecond = duration > 0f ? totalDamage / duration : 0f;
    }

    public void MergeFrom(BurnAilment previous)
    {
        float previousRemainingDamage = previous._damagePerSecond * previous._remainingDuration;
        float combinedDamage = (_damagePerSecond * _remainingDuration) + previousRemainingDamage;
        _damagePerSecond = _remainingDuration > 0f ? combinedDamage / _remainingDuration : 0f;
        _timeSinceLastTick = previous._timeSinceLastTick;
    }

    // Ticks in fixed TickInterval chunks rather than every physics frame - the alternative was a
    // real chunk of damage, once every second, vs. dozens of ~1-damage TakeDamage calls a second.
    public override bool Tick(Enemy enemy, float delta)
    {
        _remainingDuration -= delta;
        _timeSinceLastTick += delta;

        while (_timeSinceLastTick >= TickInterval)
        {
            _timeSinceLastTick -= TickInterval;
            enemy.TakeDamage(new DamagePacket { FireDamage = _damagePerSecond * TickInterval }, updateStatus: false);
        }

        return _remainingDuration > 0f;
    }
}
