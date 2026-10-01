// Left by MeltRune: deals the melt once, on its first tick - outside the hit that set it off - then
// lingers as the cooldown that keeps Melt from triggering on this enemy again until it runs out.
// Carries no element, so it isn't an ailment for anything that acts on those.
public class MeltedDebuff : Debuff
{
    private readonly float _damage;
    private readonly Attribution _attribution;
    private float _remainingDuration;
    private bool _dealt;

    public MeltedDebuff(float damage, float cooldown, Attribution attribution)
    {
        _damage = damage;
        _remainingDuration = cooldown;
        _attribution = attribution;
    }

    public override DebuffType Type => DebuffType.Melted;

    // Melt can't land while one is here, so a second never arrives to compete.
    public override DebuffApplication ResolveAgainst(Debuff existing) => DebuffApplication.Discard;

    public override bool Tick(Enemy enemy, float delta)
    {
        if (!_dealt)
        {
            _dealt = true;
            enemy.TakeDamage(new Hit
            {
                Damage = new DamagePacket { FireDamage = _damage },
                Attribution = _attribution,
                Type = HitType.DamageOverTime
            });
        }

        _remainingDuration -= delta;
        return _remainingDuration > 0f;
    }
}
