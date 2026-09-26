using Godot;
using System.Collections.Generic;

public partial class Tank : Sentinel
{
    private enum BlockState { Idle, Armed, Immune, Cooldown }

    [Export] public Node3D SwordOrbit { get; private set; }
    [Export] public Node3D SwordTilt { get; private set; }
    [Export] public Area3D SwordHitArea { get; private set; }
    [Export] public Node3D ShieldOrbit { get; private set; }
    [Export] public MeshInstance3D Shield { get; private set; }
    [Export] public float SwingLeftAngleDegrees = 200f;
    [Export] public float SwingRightOvershootDegrees = -30f;
    [Export] public float IdleAngleDegrees = 0f;
    [Export] public float SwingTiltDegrees = 0f;
    [Export] public float SwingDuration = 0.22f;
    [Export] public float ReturnDelay = 0.75f;
    [Export] public float ReturnDuration = 0.25f;
    [Export] public float BlockRaiseDuration = 0.15f;
    [Export] public float BlockImmunityDuration = 2f;
    [Export] public float BlockCooldownDuration = 4f;
    [Export] public float BlockedMoveSpeedMultiplier = 0.8f;
    [Export] public float ShieldRestAngleDegrees = 180f;
    [Export] public float ShieldBlockAngleDegrees = 90f;

    private static readonly Color ShieldNormalColor = new Color(0.4f, 0.32f, 0.15f);
    private static readonly Color ShieldImmuneColor = new Color(1f, 1f, 1f);
    private static readonly Color ShieldCooldownColor = new Color(0.35f, 0.05f, 0.05f);

    public override CharacterId Id => CharacterId.Tank;
    public override float Speed => base.Speed * (_blockState == BlockState.Armed ? BlockedMoveSpeedMultiplier : 1f);

    protected override bool IsInCombatAction =>
        _blockState == BlockState.Armed || (CanUseCombatInput && Input.IsActionPressed("attack"));

    // README's heat dial: a signed gauge with equilibrium at 0. Above it the sword adds Fire and
    // armor falls; below it armor rises. It never drifts - only the kit and hits taken move it -
    // and carries across waves, clearing only on death.
    public const float HeatLimit = 100f;

    [Export] public float MaxHeatFireFraction = 0.2f;
    [Export] public float MaxHeatArmorShift = 0.2f;
    [Export] public float HeatPerPercentHealthTaken = 1f;

    public float Heat { get; private set; }

    public void AddHeat(float amount)
    {
        Heat = Mathf.Clamp(Heat + amount, -HeatLimit, HeatLimit);
    }

    // One formula for both sides: positive heat lowers armor, negative heat raises it.
    protected override float ArmorMultiplier => 1f - MaxHeatArmorShift * Heat / HeatLimit;

    protected override void AddExtraElementalDamage(ref DamagePacket damage, float baseDamage)
    {
        if (Heat > 0f)
        {
            damage.AddElement(Tag.Fire, baseDamage * MaxHeatFireFraction * Heat / HeatLimit);
        }
    }

    protected override void OnDied()
    {
        Heat = 0f;
    }

    private StandardMaterial3D _shieldMaterial;
    private float _attackCooldown;
    private float _returnTimer;
    private bool _idleReturnStarted;
    private float _idleTiltRotationZ;
    private bool _swingTowardLeft = true;
    private Tween _swordTween;
    private Tween _shieldTween;
    private BlockState _blockState = BlockState.Idle;
    private float _blockStateTimer;
    private readonly HashSet<Enemy> _hitThisSwing = new();

    public override void _Ready()
    {
        base._Ready();
        _idleTiltRotationZ = SwordTilt.Rotation.Z;
        SwordHitArea.Monitoring = false;
        SwordHitArea.BodyEntered += OnSwordHitBody;

        _shieldMaterial = new StandardMaterial3D
        {
            AlbedoColor = ShieldNormalColor,
            EmissionEnabled = true,
            Emission = ShieldNormalColor,
            EmissionEnergyMultiplier = 0f
        };
        Shield.MaterialOverride = _shieldMaterial;
    }

    private void SetShieldColor(Color color, float emissionEnergy)
    {
        _shieldMaterial.AlbedoColor = color;
        _shieldMaterial.Emission = color;
        _shieldMaterial.EmissionEnergyMultiplier = emissionEnergy;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsActive)
        {
            return;
        }

        if (UpdateDeath((float)delta))
        {
            return;
        }

        UpdateShieldRegen((float)delta);
        UpdateHealthRegen((float)delta);

        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        ApplyGroundMovement(delta, inputDir);

        UpdateBlockState((float)delta);

        _attackCooldown -= (float)delta;

        bool isBlocking = _blockState == BlockState.Armed;

        if (!isBlocking && CanUseWeaponInput && Input.IsActionPressed("attack") && _attackCooldown <= 0f)
        {
            _attackCooldown = 1f / AttackSpeed;
            Swing();
        }

        if (_returnTimer > 0f)
        {
            _returnTimer -= (float)delta;

            if (_returnTimer <= 0f && !_idleReturnStarted)
            {
                BeginReturnToIdle();
            }
        }
    }

    private void UpdateBlockState(float delta)
    {
        switch (_blockState)
        {
            case BlockState.Idle:
                if (CanUseCombatInput && Input.IsActionPressed("alt_action"))
                {
                    _blockState = BlockState.Armed;
                    RaiseShield();
                }
                break;

            case BlockState.Armed:
                if (!Input.IsActionPressed("alt_action"))
                {
                    _blockState = BlockState.Idle;
                    LowerShield();
                }
                break;

            case BlockState.Immune:
                _blockStateTimer -= delta;
                if (_blockStateTimer <= 0f)
                {
                    _blockState = BlockState.Cooldown;
                    _blockStateTimer = BlockCooldownDuration;
                    LowerShield();
                    SetShieldColor(ShieldCooldownColor, 0.3f);
                    Shield.Scale = Vector3.One;
                }
                break;

            case BlockState.Cooldown:
                _blockStateTimer -= delta;
                if (_blockStateTimer <= 0f)
                {
                    _blockState = BlockState.Idle;
                    SetShieldColor(ShieldNormalColor, 0f);
                }
                break;
        }
    }

    private void RaiseShield()
    {
        _shieldTween?.Kill();
        _shieldTween = CreateTween();
        _shieldTween.TweenProperty(ShieldOrbit, "rotation:y", Mathf.DegToRad(ShieldBlockAngleDegrees), BlockRaiseDuration);
    }

    private void LowerShield()
    {
        _shieldTween?.Kill();
        _shieldTween = CreateTween();
        _shieldTween.TweenProperty(ShieldOrbit, "rotation:y", Mathf.DegToRad(ShieldRestAngleDegrees), BlockRaiseDuration);
    }

    // Always sweeps toward whichever of the two extremes (right/idle, or far-left) the sword isn't
    // currently at, so repeated attacks pendulum back and forth instead of resetting between hits.
    // SwordTilt flattens the blade for the swing, independent of the left-right sweep. _returnTimer
    // (checked every frame in _PhysicsProcess) is what brings it back to idle if no follow-up attack
    // lands within the grace window - not a chained tween callback, so there's no race between that
    // window and the attack cooldown.
    private void Swing()
    {
        _swordTween?.Kill();
        _hitThisSwing.Clear();
        SwordHitArea.Monitoring = true;
        _idleReturnStarted = false;
        _returnTimer = SwingDuration + ReturnDelay;

        float targetOrbitDegrees = _swingTowardLeft ? SwingLeftAngleDegrees : SwingRightOvershootDegrees;
        _swingTowardLeft = !_swingTowardLeft;

        _swordTween = CreateTween();
        _swordTween.SetParallel(true);
        _swordTween.TweenProperty(SwordOrbit, "rotation:y", Mathf.DegToRad(targetOrbitDegrees), SwingDuration)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _swordTween.TweenProperty(SwordTilt, "rotation:z", Mathf.DegToRad(SwingTiltDegrees), SwingDuration)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _swordTween.Chain().TweenCallback(Callable.From(() => SwordHitArea.Monitoring = false));
    }

    private void BeginReturnToIdle()
    {
        _idleReturnStarted = true;
        _swingTowardLeft = true;
        _swordTween?.Kill();
        _swordTween = CreateTween();
        _swordTween.SetParallel(true);
        _swordTween.TweenProperty(SwordOrbit, "rotation:y", Mathf.DegToRad(IdleAngleDegrees), ReturnDuration)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _swordTween.TweenProperty(SwordTilt, "rotation:z", _idleTiltRotationZ, ReturnDuration)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    private void OnSwordHitBody(Node3D body)
    {
        if (body is Enemy enemy && _hitThisSwing.Add(enemy))
        {
            enemy.TakeDamage(new Hit { Damage = RollAttackDamage(AttackDamage), Attribution = HitAttribution });
        }
    }

    public override void TakeDamage(Hit hit)
    {
        if (IsDodgeImmune)
        {
            return;
        }

        if (_blockState == BlockState.Armed)
        {
            _blockState = BlockState.Immune;
            _blockStateTimer = BlockImmunityDuration;
            SetShieldColor(ShieldImmuneColor, 2f);
            Shield.Scale = Vector3.One * 1.5f;
            return;
        }

        if (_blockState == BlockState.Immune)
        {
            return;
        }

        // Counted before mitigation, so armor never slows how fast getting hit heats the Tank up.
        if (!IsDead)
        {
            AddHeat(hit.Damage.Total / MaxHealth * 100f * HeatPerPercentHealthTaken);
        }

        base.TakeDamage(hit);
    }
}
