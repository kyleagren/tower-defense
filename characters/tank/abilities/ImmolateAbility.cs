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
    [Export] public PackedScene EruptionScene { get; private set; }

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

        Rpc(MethodName.BeginChannelVisual, _radius);
    }

    // On every machine, the Tank's own included. A child of the Tank, so it goes with him if he's torn
    // down mid-channel. The radius is kept here too, since the ring finds its targets through
    // FindErupting.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void BeginChannelVisual(float radius)
    {
        _radius = radius;
        _channel = ChannelScene.Instantiate<ImmolateChannelVisual>();
        Character.AddChild(_channel);
        _channel.Begin(_radius, ChannelDuration, FindErupting);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void EndChannelVisual()
    {
        if (_channel != null && IsInstanceValid(_channel))
        {
            _channel.QueueFree();
        }

        _channel = null;
    }

    public override void OnCharacterDied()
    {
        if (IsActive)
        {
            EndChannel();
        }
    }

    public override void OnBuildStarted()
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
        }
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

            if (eruptionDamage > 0f || enemy.HasDebuff(DebuffType.Burn))
            {
                enemy.ApplyDebuff(new ImmolationAilment(eruptionDamage, ImmolationPacing, immolationDuration, Character.HitAttribution));
            }
        }

        foreach (Vector3 origin in origins)
        {
            if (eruptionDamage > 0f)
            {
                HitAround(origin, eruptionRadius, eruptionDamage);
            }

            Rpc(MethodName.PlayEruption, origin, eruptionRadius);
        }

        Wielder.AddHeat(-(Wielder.Heat + Tank.HeatLimit));
        EndChannel();
        StartCooldown();
    }

    // Each enemy is pushed away from the eruption, flat.
    private void HitAround(Vector3 origin, float radius, float damage)
    {
        foreach (Enemy enemy in EnemyTargeting.FindAllWithinFlat(origin, radius))
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

    private List<Enemy> FindErupting() =>
        EnemyTargeting.FindAllWithinFlat(Character.GlobalPosition, _radius,
            enemy => enemy.Status == Tag.Fire || enemy.HasDebuff(DebuffType.Burn));

    // On every machine, the Tank's own included.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void PlayEruption(Vector3 position, float radius)
    {
        ImmolateEruption eruption = EruptionScene.Instantiate<ImmolateEruption>();
        LevelContext.Current.LevelRoot.AddChild(eruption);
        eruption.Show(position, radius);
    }

    private void EndChannel()
    {
        IsActive = false;
        Wielder.ExitImmolateChannel();
        Rpc(MethodName.EndChannelVisual);
    }
}
