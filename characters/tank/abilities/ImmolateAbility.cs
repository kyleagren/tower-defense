using Godot;
using System.Collections.Generic;

// README's Immolate (Characters). A short planted channel with sword and shield set down, then every
// enemy within Radius carrying a Fire Status or a Burn erupts. Each one's Status and Burn are
// consumed into an Immolation, and it blasts Fire over everything within EruptionRadius of it. Going
// off takes all of the Tank's heat and drops him to the bottom of the dial; eruption damage scales
// with the heat it took. Cancelling - a second press, dying, the wave ending - costs nothing.
public partial class ImmolateAbility : Ability
{
    [Export] public override float BaseCooldown { get; protected set; } = 12f;
    [Export] public float ChannelDuration { get; private set; } = 1f;
    [Export] public float Radius { get; private set; } = 14f;
    [Export] public float EruptionRadius { get; private set; } = 3f;
    // Each eruption's damage at full heat, as a multiple of the sword's attack damage.
    [Export] public float EruptionDamageAtMaxHeat { get; private set; } = 1.5f;
    // Immolation's tick size is its base damage spread over ImmolationPacing; it then keeps ticking
    // at that size for ImmolationDuration, which Duration scales.
    [Export] public float ImmolationPacing { get; private set; } = 2f;
    [Export] public float ImmolationDuration { get; private set; } = 4f;
    // Launches anything an eruption kills, outward from it.
    [Export] public float KillLaunchImpulse { get; private set; } = 200f;
    [Export] public PackedScene ChannelScene { get; private set; }
    [Export] public PackedScene ExplosionScene { get; private set; }

    public override string DisplayName => "Immolate";
    public override string Description => "Call the fire out of every burning enemy nearby, erupting and immolating each one.";

    public override float ActiveFraction => _remaining / ChannelDuration;
    public override bool CanToggleOff => true;
    public override bool LocksOutOtherAbilities => IsActive;

    private float _remaining;
    private float _radius;
    private ImmolateChannelVisual _channel;

    private Tank Wielder => (Tank)Character;

    public override bool TryActivate()
    {
        if (IsActive)
        {
            EndChannel();
            return true;
        }

        return base.TryActivate();
    }

    protected override bool CanActivate()
    {
        return Wielder.Heat > 0f;
    }

    protected override void Activate()
    {
        IsActive = true;
        _remaining = ChannelDuration;
        _radius = ScaledArea(Radius);
        Wielder.EnterImmolateChannel();

        // A child of the Tank, so it goes with him if he's torn down mid-channel.
        _channel = ChannelScene.Instantiate<ImmolateChannelVisual>();
        Character.AddChild(_channel);
        _channel.Begin(_radius);
        _channel.MarkTargets(FindErupting());
    }

    public override void OnCharacterDied()
    {
        if (IsActive)
        {
            EndChannel();
        }
    }

    public override void OnCharacterDeactivated()
    {
        if (IsActive)
        {
            EndChannel();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (!IsActive)
        {
            return;
        }

        _remaining -= (float)delta;

        if (_remaining <= 0f)
        {
            Erupt();
            return;
        }

        _channel.SetProgress(1f - _remaining / ChannelDuration);
        _channel.MarkTargets(FindErupting());
    }

    // Who erupts is decided as the channel ends, so an enemy set alight mid-channel still counts.
    private void Erupt()
    {
        List<Enemy> erupting = FindErupting();
        float heatFraction = Mathf.Max(Wielder.Heat, 0f) / Tank.HeatLimit;
        float eruptionDamage = ScaledDamage(Character.AttackDamage * EruptionDamageAtMaxHeat * heatFraction);
        float eruptionRadius = ScaledArea(EruptionRadius);
        float immolationDuration = ScaledDuration(ImmolationDuration);

        // Every Immolation lands before any eruption does, and each eruption goes off from where its
        // enemy stood even if a neighbor's eruption has already killed it.
        List<Vector3> origins = new List<Vector3>();

        foreach (Enemy enemy in erupting)
        {
            origins.Add(enemy.CenterMass);
            float immolationDamage = eruptionDamage + ConsumeFire(enemy);

            if (immolationDamage > 0f)
            {
                enemy.ApplyDebuff(new ImmolationAilment(immolationDamage, ImmolationPacing, immolationDuration, Character.HitAttribution));
            }
        }

        foreach (Vector3 origin in origins)
        {
            if (eruptionDamage > 0f)
            {
                HitAround(origin, eruptionRadius, eruptionDamage);
            }

            PlayExplosion(origin, eruptionRadius);
        }

        Wielder.AddHeat(-(Wielder.Heat + Tank.HeatLimit));
        EndChannel();
        StartCooldown();
    }

    // Measured flat, and collected up front - a kill unregisters from the list being walked.
    private void HitAround(Vector3 origin, float radius, float damage)
    {
        List<Enemy> targets = new List<Enemy>();

        foreach (Enemy enemy in LevelContext.Current.Enemies.Active)
        {
            if (FlatDistanceSquared(enemy.GlobalPosition, origin) <= radius * radius)
            {
                targets.Add(enemy);
            }
        }

        foreach (Enemy enemy in targets)
        {
            Vector3 away = enemy.GlobalPosition - origin;
            away.Y = 0f;
            away = away == Vector3.Zero ? Vector3.Up : away.Normalized();

            enemy.TakeDamage(new Hit
            {
                Damage = new DamagePacket { FireDamage = damage, Source = DamageSource.Weapon },
                Attribution = Character.HitAttribution,
                Type = HitType.Unmarked,
                RagdollImpulse = (away + Vector3.Up * 0.3f).Normalized() * KillLaunchImpulse
            });
        }
    }

    private List<Enemy> FindErupting()
    {
        List<Enemy> found = new List<Enemy>();
        Vector3 origin = Character.GlobalPosition;

        foreach (Enemy enemy in LevelContext.Current.Enemies.Active)
        {
            if (FlatDistanceSquared(enemy.GlobalPosition, origin) <= _radius * _radius
                && (enemy.Status == Tag.Fire || FindBurn(enemy) != null))
            {
                found.Add(enemy);
            }
        }

        return found;
    }

    private static BurnAilment FindBurn(Enemy enemy)
    {
        foreach (Debuff debuff in enemy.Debuffs)
        {
            if (debuff is BurnAilment burn)
            {
                return burn;
            }
        }

        return null;
    }

    // Takes the enemy's Fire Status and Burn, returning what the Burn still had left to deal.
    private static float ConsumeFire(Enemy enemy)
    {
        if (enemy.Status == Tag.Fire)
        {
            enemy.RemoveStatus();
        }

        BurnAilment burn = FindBurn(enemy);

        if (burn == null)
        {
            return 0f;
        }

        enemy.RemoveDebuff(burn);
        return burn.RemainingDamage;
    }

    private static float FlatDistanceSquared(Vector3 a, Vector3 b)
    {
        Vector3 offset = a - b;
        offset.Y = 0f;
        return offset.LengthSquared();
    }

    private void PlayExplosion(Vector3 position, float radius)
    {
        ExplosionVisual explosion = ExplosionScene.Instantiate<ExplosionVisual>();
        LevelContext.Current.LevelRoot.AddChild(explosion);
        explosion.Show(position, radius);
    }

    private void EndChannel()
    {
        IsActive = false;
        Wielder.ExitImmolateChannel();

        if (_channel != null && IsInstanceValid(_channel))
        {
            _channel.QueueFree();
        }

        _channel = null;
    }
}
