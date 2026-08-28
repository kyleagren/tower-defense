public class BurnAilment : Ailment
{
    private float _damagePerSecond;
    private float _remainingDuration;

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
    }

    public override bool Tick(Enemy enemy, float delta)
    {
        _remainingDuration -= delta;
        enemy.TakeDamage(new DamagePacket { FireDamage = _damagePerSecond * delta }, updateStatus: false);
        return _remainingDuration > 0f;
    }
}
