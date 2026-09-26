using Godot;

// Immolate's ailment. Its tick size comes from spreading baseDamage over pacingDuration, and it keeps
// ticking at that size for duration - so a duration past the pacing deals more than baseDamage in
// total. Ticks are counted rather than timed out, so a Duration bonus earns an extra tick exactly at
// each TickInterval breakpoint and never loses the last one to float drift.
public class ImmolationAilment : Debuff
{
    private const float TickInterval = 0.5f;

    private static readonly Tag[] _elements = { Tag.Fire };

    private readonly Attribution _attribution;
    private readonly float _damagePerTick;
    private int _ticksRemaining;
    private float _timeSinceLastTick;

    public ImmolationAilment(float baseDamage, float pacingDuration, float duration, Attribution attribution)
    {
        _damagePerTick = pacingDuration > 0f ? baseDamage / pacingDuration * TickInterval : 0f;
        // The epsilon keeps a duration that's a whole number of ticks from rounding down one.
        _ticksRemaining = Mathf.FloorToInt(duration / TickInterval + 0.001f);
        _attribution = attribution;
    }

    public override Tag[] Elements => _elements;

    public override bool Tick(Enemy enemy, float delta)
    {
        _timeSinceLastTick += delta;

        while (_timeSinceLastTick >= TickInterval && _ticksRemaining > 0)
        {
            _timeSinceLastTick -= TickInterval;
            _ticksRemaining--;
            enemy.TakeDamage(new Hit
            {
                Damage = new DamagePacket { FireDamage = _damagePerTick },
                Attribution = _attribution,
                Type = HitType.DamageOverTime
            });
        }

        return _ticksRemaining > 0;
    }
}
