using Godot;

// Left by OverloadRune: bursts once, on its first tick - outside the hit that set it off - damaging
// everything around this enemy and launching it into the air, then lingers as the cooldown that keeps
// Overload from triggering on it again until it runs out. Everything the burst launches gets a quiet
// one of its own, so a pack goes up once and can't be juggled by its neighbours' bursts.
public class OverloadedDebuff : Debuff
{
    private readonly float _damage;
    private readonly float _radius;
    private readonly float _launchImpulse;
    // Radians off straight up, tipped away from the centre.
    private readonly float _launchOutward;
    private readonly float _cooldown;
    private readonly PackedScene _burstScene;
    private readonly Attribution _attribution;
    private float _remainingDuration;
    private bool _burst;

    public OverloadedDebuff(float damage, float radius, float launchImpulse, float launchOutwardDegrees, float cooldown, PackedScene burstScene, Attribution attribution)
    {
        _damage = damage;
        _radius = radius;
        _launchImpulse = launchImpulse;
        _launchOutward = Mathf.DegToRad(launchOutwardDegrees);
        _cooldown = cooldown;
        _remainingDuration = cooldown;
        _burstScene = burstScene;
        _attribution = attribution;
    }

    // Left on everything a burst caught: no burst of its own, just the cooldown, so an enemy launched
    // once can't be launched again - or set off a burst itself - until it runs out.
    public OverloadedDebuff(float cooldown)
    {
        _remainingDuration = cooldown;
        _burst = true;
    }

    public override DebuffType Type => DebuffType.Overloaded;

    // Overload can't land while one is here, so a second never arrives to compete.
    public override DebuffApplication ResolveAgainst(Debuff existing) => DebuffApplication.Discard;

    public override bool Tick(Enemy enemy, float delta)
    {
        if (!_burst)
        {
            _burst = true;
            Burst(enemy);
        }

        _remainingDuration -= delta;
        return _remainingDuration > 0f;
    }

    // Unmarked, so the burst sets no Status of its own.
    private void Burst(Enemy center)
    {
        if (_burstScene != null)
        {
            LevelContext.Current.SpellEffects.ShowExplosion(_burstScene, center.CenterMass, _radius);
        }

        DamagePacket damage = new DamagePacket { LightningDamage = _damage };

        foreach (Enemy enemy in EnemyTargeting.FindAllWithinFlat(center.GlobalPosition, _radius, enemy => !enemy.IsDead))
        {
            // Read before this burst marks it, so only a mark from an earlier burst protects it.
            bool alreadyOverloaded = enemy != center && enemy.HasDebuff(DebuffType.Overloaded);

            enemy.TakeDamage(new Hit { Damage = damage, Attribution = _attribution, Type = HitType.Unmarked });

            if (enemy.IsDead || alreadyOverloaded)
            {
                continue;
            }

            Vector3 away = enemy.GlobalPosition - center.GlobalPosition;
            away.Y = 0f;

            // Up, tipped outward from the centre by a few degrees; the centre itself goes straight up.
            Vector3 direction = away.LengthSquared() > 0.0001f
                ? (Vector3.Up * Mathf.Cos(_launchOutward) + away.Normalized() * Mathf.Sin(_launchOutward)).Normalized()
                : Vector3.Up;

            enemy.ApplyKnockback(direction * _launchImpulse);

            if (enemy != center)
            {
                enemy.ApplyDebuff(new OverloadedDebuff(_cooldown));
            }
        }
    }
}
