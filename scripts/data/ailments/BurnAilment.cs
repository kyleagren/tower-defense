public class BurnAilment : Ailment
{
    private float damagePerSecond;
    private float remainingDuration;

    public BurnAilment(float totalDamage, float duration)
    {
        remainingDuration = duration;
        damagePerSecond = duration > 0f ? totalDamage / duration : 0f;
    }

    public void MergeFrom(BurnAilment previous)
    {
        float previousRemainingDamage = previous.damagePerSecond * previous.remainingDuration;
        float combinedDamage = (damagePerSecond * remainingDuration) + previousRemainingDamage;
        damagePerSecond = remainingDuration > 0f ? combinedDamage / remainingDuration : 0f;
    }

    public override bool Tick(Enemy enemy, float delta)
    {
        remainingDuration -= delta;
        enemy.TakeDamage(new DamagePacket { FireDamage = damagePerSecond * delta }, updateStatus: false);
        return remainingDuration > 0f;
    }
}
