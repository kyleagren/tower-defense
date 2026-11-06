using Godot;
using System.Collections.Generic;

// README's Retaliation (Characters). For Duration, every enemy that comes within Radius is taunted
// onto the Tank, who takes DamageReduction less damage throughout. The raw damage of every hit that
// lands - before any mitigation, and not counting blocked or dodged ones - is recorded, then released
// as a Fire blast on everything within Radius: BlastFraction of it to each, capped per enemy at the
// Tank's max health, so fodder dies and brutes only dent. Heat lands with the blast rather than when
// the window opens, so taunting never costs armor while everything is hitting him. The Tank can't
// move for the window. A second press, or dying, ends it early and releases the blast on the spot.
public partial class RetaliationAbility : Ability
{
    [Export] public override float BaseCooldown { get; protected set; } = 15f;
    [Export] public float Radius { get; private set; } = 10f;
    [Export] public float Duration { get; private set; } = 5f;
    // Applied before armor, so the two compound without ever reaching full immunity.
    [Export] public float DamageReduction { get; private set; } = 0.4f;
    [Export] public float BlastFraction { get; private set; } = 0.7f;
    // Per enemy, as a multiple of the Tank's max health.
    [Export] public float BlastCapMaxHealthMultiple { get; private set; } = 1f;
    [Export] public float HeatGain { get; private set; } = 40f;
    // Launches anything the blast kills, outward from the Tank - the same push Shield Slam gives.
    [Export] public float KillLaunchImpulse { get; private set; } = 200f;
    [Export] public PackedScene WaveScene { get; private set; }
    [Export] public PackedScene ChargeScene { get; private set; }
    [Export] public Color BlastWaveColor { get; private set; } = new Color(1f, 0.5f, 0.1f, 0.45f);

    // How often the window looks for newcomers to taunt.
    private const float TauntInterval = 0.1f;

    public override string DisplayName => "Retaliation";
    public override string Description => "Taunt nearby enemies and take less damage, then release what they dealt as a blast of fire.";

    public override float ActiveFraction => _activeDuration <= 0f ? 0f : _remaining / _activeDuration;
    public override bool CanToggleOff => true;
    public override bool LocksOutOtherAbilities => IsActive;

    public float IncomingDamageMultiplier => IsActive ? 1f - DamageReduction : 1f;

    private float _activeDuration;
    private float _remaining;
    private float _radius;
    private float _tauntTimer;
    private float _recordedDamage;
    private RetaliationChargeCircle _chargeCircle;
    private readonly HashSet<Enemy> _taunted = new();

    private Tank Wielder => (Tank)Character;

    // A second press ends the window early and releases the blast on the spot, as the window running
    // out would.
    public override bool TryActivate()
    {
        if (IsActive)
        {
            Release();
            return true;
        }

        return base.TryActivate();
    }

    protected override void Activate()
    {
        IsActive = true;
        _activeDuration = ScaledDuration(Duration);
        _remaining = _activeDuration;
        _radius = ScaledArea(Radius);
        _recordedDamage = 0f;
        _tauntTimer = TauntInterval;
        _taunted.Clear();

        Wielder.EnterRetaliationStance();
        TauntNewcomers();

        Rpc(MethodName.BeginChargeCircle, Character.GlobalPosition, _radius, _activeDuration);
    }

    // On every machine, the Tank's own included.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void BeginChargeCircle(Vector3 origin, float radius, float duration)
    {
        _chargeCircle = ChargeScene.Instantiate<RetaliationChargeCircle>();
        LevelContext.Current.LevelRoot.AddChild(_chargeCircle);
        _chargeCircle.Begin(origin, radius, duration);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void EndChargeCircle()
    {
        FreeChargeCircle();
    }

    // The circle lives in the scene, not under this node, so a Sentinel torn down mid-window (hub
    // character swap) would otherwise leave it behind.
    public override void _ExitTree()
    {
        FreeChargeCircle();
    }

    private void FreeChargeCircle()
    {
        if (_chargeCircle != null && IsInstanceValid(_chargeCircle))
        {
            _chargeCircle.QueueFree();
        }

        _chargeCircle = null;
    }

    // The raw damage of a hit that landed, before any mitigation.
    public void RecordHit(float rawDamage)
    {
        if (IsActive)
        {
            _recordedDamage += rawDamage;
        }
    }

    public override void OnCharacterDied()
    {
        if (IsActive)
        {
            Release();
        }
    }

    public override void OnBuildStarted()
    {
        if (IsActive)
        {
            EndWindow();
            StartCooldown();
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
            Release();
            return;
        }

        _tauntTimer -= (float)delta;

        if (_tauntTimer <= 0f)
        {
            _tauntTimer = TauntInterval;
            TauntNewcomers();
        }
    }

    // Each taunt runs to the end of the window, so an enemy caught once stays on the Tank even if
    // it's shoved back out of range - and is taunted only once, since a copy can't see its own taunt.
    private void TauntNewcomers()
    {
        foreach (Enemy enemy in EnemiesInRadius())
        {
            if (_taunted.Add(enemy))
            {
                enemy.ApplyDebuff(new TauntedDebuff(Character, _remaining));
            }
        }
    }

    private void EndWindow()
    {
        IsActive = false;
        Wielder.ExitRetaliationStance();
        Rpc(MethodName.EndChargeCircle);
    }

    private void Release()
    {
        EndWindow();

        float damage = Mathf.Min(ScaledDamage(_recordedDamage * BlastFraction), Character.MaxHealth * BlastCapMaxHealthMultiple);

        if (damage > 0f)
        {
            foreach (Enemy enemy in EnemiesInRadius())
            {
                Vector3 away = enemy.GlobalPosition - Character.GlobalPosition;
                away.Y = 0f;
                away = away == Vector3.Zero ? Vector3.Forward : away.Normalized();

                enemy.TakeDamage(new Hit
                {
                    Damage = new DamagePacket { FireDamage = damage, Source = DamageSource.Weapon },
                    Attribution = Character.HitAttribution,
                    RagdollImpulse = (away + Vector3.Up * 0.3f).Normalized() * KillLaunchImpulse
                });
            }
        }

        // Death already cleared heat - a blast released by dying shouldn't hand the respawn some back.
        if (!Character.IsDead)
        {
            Wielder.AddHeat(HeatGain);
        }

        Rpc(MethodName.PlayWave, Character.GlobalPosition, _radius, BlastWaveColor);
        StartCooldown();
    }

    private List<Enemy> EnemiesInRadius() => EnemyTargeting.FindAllWithinFlat(Character.GlobalPosition, _radius);

    // On every machine, the Tank's own included.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void PlayWave(Vector3 position, float radius, Color color)
    {
        ShieldSlamWave wave = WaveScene.Instantiate<ShieldSlamWave>();
        LevelContext.Current.LevelRoot.AddChild(wave);
        wave.Play(position, Vector3.Forward, radius, 360f, color);
    }
}
