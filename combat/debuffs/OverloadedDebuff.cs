using Godot;

// Left by OverloadRune: bursts once, on its first tick - outside the hit that set it off - damaging
// everything around this enemy and throwing the rest back, then lingers as the cooldown that keeps
// Overload from triggering on it again until it runs out.
public class OverloadedDebuff : Debuff
{
    private readonly float _damage;
    private readonly float _radius;
    private readonly float _knockbackImpulse;
    private readonly PackedScene _burstScene;
    private readonly Attribution _attribution;
    private float _remainingDuration;
    private bool _burst;

    public OverloadedDebuff(float damage, float radius, float knockbackImpulse, float cooldown, PackedScene burstScene, Attribution attribution)
    {
        _damage = damage;
        _radius = radius;
        _knockbackImpulse = knockbackImpulse;
        _remainingDuration = cooldown;
        _burstScene = burstScene;
        _attribution = attribution;
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

    // Unmarked, so the burst sets no Status of its own. The enemy at the centre takes the damage but
    // has no direction to be thrown in.
    private void Burst(Enemy center)
    {
        if (_burstScene != null)
        {
            LevelContext.Current.SpellEffects.ShowExplosion(_burstScene, center.CenterMass, _radius);
        }

        DamagePacket damage = new DamagePacket { LightningDamage = _damage };

        foreach (Enemy enemy in EnemyTargeting.FindAllWithinFlat(center.GlobalPosition, _radius, enemy => !enemy.IsDead))
        {
            enemy.TakeDamage(new Hit { Damage = damage, Attribution = _attribution, Type = HitType.Unmarked });

            Vector3 away = enemy.GlobalPosition - center.GlobalPosition;
            away.Y = 0f;

            if (enemy != center && !enemy.IsDead && away.LengthSquared() > 0.0001f)
            {
                enemy.ApplyKnockback(away.Normalized() * _knockbackImpulse);
            }
        }
    }
}
