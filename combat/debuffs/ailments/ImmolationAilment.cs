using Godot;

// Immolate's ailment. Its tick size comes from spreading baseDamage over pacingDuration, and it keeps
// ticking at that size for duration - so a duration past the pacing deals more than baseDamage in
// total. Ticks are counted rather than timed out, so a Duration bonus earns an extra tick exactly at
// each TickInterval breakpoint and never loses the last one to float drift. Applying it takes the
// enemy's Fire Status and Burn into its own damage.
public class ImmolationAilment : Debuff
{
    private const float TickInterval = 0.5f;

    private static readonly Tag[] _elements = { Tag.Fire };

    private readonly Attribution _attribution;
    private readonly float _pacingDuration;
    private float _baseDamage;
    private int _ticksRemaining;
    private float _timeSinceLastTick;

    public ImmolationAilment(float baseDamage, float pacingDuration, float duration, Attribution attribution)
    {
        _baseDamage = baseDamage;
        _pacingDuration = pacingDuration;
        // The epsilon keeps a duration that's a whole number of ticks from rounding down one.
        _ticksRemaining = Mathf.FloorToInt(duration / TickInterval + 0.001f);
        _attribution = attribution;
    }

    public override Tag[] Elements => _elements;

    public override DebuffType Type => DebuffType.Immolation;

    private float DamagePerTick => _pacingDuration > 0f ? _baseDamage / _pacingDuration * TickInterval : 0f;

    // Takes the enemy's Fire Status and whatever its Burn still had left to deal.
    public override void OnApplying(Enemy host)
    {
        if (host.Status == Tag.Fire)
        {
            host.RemoveStatus();
        }

        foreach (Debuff debuff in host.Debuffs)
        {
            if (debuff is BurnAilment burn)
            {
                _baseDamage += burn.RemainingDamage;
                host.RemoveDebuff(burn);
                break;
            }
        }
    }

    public override Godot.Collections.Array ToNetwork() =>
        new() { (int)Type, _baseDamage, _pacingDuration, _ticksRemaining * TickInterval };

    public static ImmolationAilment Decode(Godot.Collections.Array data, Attribution attribution) =>
        new(data[1].AsSingle(), data[2].AsSingle(), data[3].AsSingle(), attribution);

    public override bool Tick(Enemy enemy, float delta)
    {
        _timeSinceLastTick += delta;

        while (_timeSinceLastTick >= TickInterval && _ticksRemaining > 0)
        {
            _timeSinceLastTick -= TickInterval;
            _ticksRemaining--;
            enemy.TakeDamage(new Hit
            {
                Damage = new DamagePacket { FireDamage = DamagePerTick },
                Attribution = _attribution,
                Type = HitType.DamageOverTime
            });
        }

        return _ticksRemaining > 0;
    }
}
