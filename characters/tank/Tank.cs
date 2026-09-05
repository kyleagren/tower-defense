using Godot;
using System.Collections.Generic;

public partial class Tank : Sentinel
{
    private enum BlockState { Idle, Armed, Immune, Cooldown }

    [Export] public Node3D SwordOrbit { get; private set; }
    [Export] public Node3D SwordTilt { get; private set; }
    [Export] public Area3D SwordHitArea { get; private set; }
    [Export] public Node3D ShieldOrbit { get; private set; }
    [Export] public float AttackDamage = 35f;
    [Export] public float AttackSpeed = 1.2f;
    [Export] public float SwingLeftAngleDegrees = 200f;
    [Export] public float SwingRightOvershootDegrees = -30f;
    [Export] public float IdleAngleDegrees = 0f;
    [Export] public float SwingTiltDegrees = 0f;
    [Export] public float SwingDuration = 0.22f;
    [Export] public float ReturnDelay = 0.75f;
    [Export] public float ReturnDuration = 0.25f;
    [Export] public float BlockRaiseDuration = 0.15f;
    [Export] public float BlockImmunityDuration = 1f;
    [Export] public float BlockCooldownDuration = 5f;
    [Export] public float BlockedMoveSpeedMultiplier = 0.8f;
    [Export] public float ShieldRestAngleDegrees = 180f;
    [Export] public float ShieldBlockAngleDegrees = 90f;

    public override CharacterId Id => CharacterId.Tank;
    public override float Speed => base.Speed * (_blockState == BlockState.Armed ? BlockedMoveSpeedMultiplier : 1f);

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
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsActive)
        {
            return;
        }

        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        ApplyGroundMovement(delta, inputDir);

        UpdateBlockState((float)delta);

        _attackCooldown -= (float)delta;

        bool isBlocking = _blockState == BlockState.Armed;

        if (!isBlocking && Input.IsActionPressed("attack") && _attackCooldown <= 0f)
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

        UpdateDebugPoseTuning((float)delta);
    }

    // TEMPORARY: hold [ / ] to nudge heading (Y), , / . to nudge tilt (Z), ; / ' to nudge roll (X) -
    // current values print to the output console. Remove once the idle pose is dialed in.
    private void UpdateDebugPoseTuning(float delta)
    {
        float step = Mathf.DegToRad(30f) * delta;
        bool changed = false;

        if (Input.IsKeyPressed(Key.Bracketleft)) { SwordTilt.RotateY(-step); changed = true; }
        if (Input.IsKeyPressed(Key.Bracketright)) { SwordTilt.RotateY(step); changed = true; }
        if (Input.IsKeyPressed(Key.Comma)) { SwordTilt.RotateZ(-step); changed = true; }
        if (Input.IsKeyPressed(Key.Period)) { SwordTilt.RotateZ(step); changed = true; }
        if (Input.IsKeyPressed(Key.Semicolon)) { SwordTilt.RotateX(-step); changed = true; }
        if (Input.IsKeyPressed(Key.Apostrophe)) { SwordTilt.RotateX(step); changed = true; }

        if (changed)
        {
            Vector3 deg = new Vector3(Mathf.RadToDeg(SwordTilt.Rotation.X), Mathf.RadToDeg(SwordTilt.Rotation.Y), Mathf.RadToDeg(SwordTilt.Rotation.Z));
            GD.Print($"SwordTilt rotation degrees: X={deg.X:F1} Y={deg.Y:F1} Z={deg.Z:F1}");
        }
    }

    private void UpdateBlockState(float delta)
    {
        switch (_blockState)
        {
            case BlockState.Idle:
                if (Input.IsActionPressed("alt_action"))
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
                }
                break;

            case BlockState.Cooldown:
                _blockStateTimer -= delta;
                if (_blockStateTimer <= 0f)
                {
                    _blockState = BlockState.Idle;
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
            enemy.TakeDamage(new DamagePacket { PhysicalDamage = AttackDamage });
        }
    }

    public override void TakeDamage(DamagePacket damage, Vector3 impulse = default)
    {
        if (_blockState == BlockState.Armed)
        {
            _blockState = BlockState.Immune;
            _blockStateTimer = BlockImmunityDuration;
            return;
        }

        if (_blockState == BlockState.Immune)
        {
            return;
        }

        base.TakeDamage(damage, impulse);
    }
}
