using Godot;

// Applied by FrostburnRune: Cold damage over time with a slight slow - less damage than a Burn, in
// exchange for the slow. Only ever starts on the host, from a reaction, so it never travels.
public class FrostburnAilment : Debuff
{
    private const float TickInterval = 1f;

    private static readonly Tag[] _elements = { Tag.Cold };

    private readonly Attribution _attribution;
    private float _damagePerSecond;
    private float _slowPercent;
    private float _remainingDuration;
    private float _timeSinceLastTick;

    // attribution is whoever set it off, so every tick is credited to them.
    public FrostburnAilment(float totalDamage, float slowPercent, float duration, Attribution attribution)
    {
        _remainingDuration = duration;
        _damagePerSecond = duration > 0f ? totalDamage / duration : 0f;
        _slowPercent = Mathf.Clamp(slowPercent, 0f, 1f);
        _attribution = attribution;
    }

    public override Tag[] Elements => _elements;

    public override DebuffType Type => DebuffType.Frostburn;

    public override float SpeedMultiplier => 1f - _slowPercent;

    // Merges its damage like a Burn rather than competing, and keeps the stronger slow.
    public override DebuffApplication ResolveAgainst(Debuff existing)
    {
        FrostburnAilment previous = (FrostburnAilment)existing;
        float combinedDamage = _damagePerSecond * _remainingDuration + previous._damagePerSecond * previous._remainingDuration;

        _damagePerSecond = _remainingDuration > 0f ? combinedDamage / _remainingDuration : 0f;
        _slowPercent = Mathf.Max(_slowPercent, previous._slowPercent);
        _timeSinceLastTick = previous._timeSinceLastTick;
        return DebuffApplication.Replace;
    }

    public override bool Tick(Enemy enemy, float delta)
    {
        _remainingDuration -= delta;
        _timeSinceLastTick += delta;

        while (_timeSinceLastTick >= TickInterval)
        {
            _timeSinceLastTick -= TickInterval;
            enemy.TakeDamage(new Hit
            {
                Damage = new DamagePacket { ColdDamage = _damagePerSecond * TickInterval },
                Attribution = _attribution,
                Type = HitType.DamageOverTime
            });
        }

        return _remainingDuration > 0f;
    }
}
