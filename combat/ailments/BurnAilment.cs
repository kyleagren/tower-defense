public class BurnAilment : Ailment
{
    private const float TickInterval = 1f;

    private readonly Attribution _attribution;
    private float _damagePerSecond;
    private float _remainingDuration;
    private float _timeSinceLastTick;

    // attribution is whoever started the burn, so every tick is credited to them.
    public BurnAilment(float totalDamage, float duration, Attribution attribution)
    {
        _remainingDuration = duration;
        _damagePerSecond = duration > 0f ? totalDamage / duration : 0f;
        _attribution = attribution;
    }

    // Merges rather than competing: two burns on one enemy combine their remaining damage into a
    // single instance instead of the stronger erasing the weaker. The merged burn is this, the
    // incoming one, so its credit goes to whoever lit it most recently.
    public override AilmentApplication ResolveAgainst(Ailment existing)
    {
        MergeFrom((BurnAilment)existing);
        return AilmentApplication.Replace;
    }

    private void MergeFrom(BurnAilment previous)
    {
        float previousRemainingDamage = previous._damagePerSecond * previous._remainingDuration;
        float combinedDamage = (_damagePerSecond * _remainingDuration) + previousRemainingDamage;
        _damagePerSecond = _remainingDuration > 0f ? combinedDamage / _remainingDuration : 0f;
        _timeSinceLastTick = previous._timeSinceLastTick;
    }

    // Ticks in fixed TickInterval chunks rather than every physics frame: one meaningful chunk of
    // damage per interval, instead of dozens of ~1-damage TakeDamage calls a second.
    public override bool Tick(Enemy enemy, float delta)
    {
        _remainingDuration -= delta;
        _timeSinceLastTick += delta;

        while (_timeSinceLastTick >= TickInterval)
        {
            _timeSinceLastTick -= TickInterval;
            enemy.TakeDamage(new Hit
            {
                Damage = new DamagePacket { FireDamage = _damagePerSecond * TickInterval },
                Attribution = _attribution,
                Type = HitType.DamageOverTime
            });
        }

        return _remainingDuration > 0f;
    }
}
