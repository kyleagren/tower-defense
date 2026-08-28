public class BurnAilment : Ailment
{
    private readonly float damagePerSecond;
    private float remainingDuration;

    public BurnAilment(float damagePerSecond, float duration)
    {
        this.damagePerSecond = damagePerSecond;
        remainingDuration = duration;
    }

    public override bool Tick(Enemy enemy, float delta)
    {
        remainingDuration -= delta;
        enemy.TakeDamage(new DamagePacket { FireDamage = damagePerSecond * delta }, updateStatus: false);
        return remainingDuration > 0f;
    }
}
