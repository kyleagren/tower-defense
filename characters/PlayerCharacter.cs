using Godot;

public abstract partial class PlayerCharacter : CharacterBody3D
{
    [Export] public PlayerId OwnerId { get; private set; } = PlayerId.P1;
    [Export] public float BaseSpeed = 5.0f;
    [Export] public float JumpVelocity = 4.5f;
    // Jumps allowed in mid-air before landing again - 1 is a double jump. Walking off a ledge still
    // leaves them all.
    [Export] public int AirJumps = 1;
    [Export] public float StepHeight = 0.35f;
    [Export] public float SprintSpeedMultiplier = 1.3f;
    [Export] public float SprintFovBonus = 5f;
    [Export] public float FovTransitionSpeed = 10f;
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float StickLookSpeedDegrees = 180f;
    [Export] public float MinPitchDegrees = -89f;
    [Export] public float MaxPitchDegrees = 89f;
    [Export] public SpringArm3D SpringArm { get; private set; }
    [Export] public Camera3D Camera { get; private set; }
    [Export] public MeshInstance3D BodyMesh { get; private set; }

    public bool IsActive => _active;

    // Whether this machine drives this body. Offline that's every body; in a session, only the
    // owning player's machine does.
    public bool IsLocallyControlled => IsMultiplayerAuthority();

    // True only while this character is both the active combatant and no UI panel (inventory,
    // build menu, etc.) is capturing input instead - combat actions check this rather than IsActive
    // alone, so clicking inside an open panel doesn't also swing a sword or fire a gun.
    protected bool CanUseCombatInput => CanAct && !UiPanelStack.AnyOpen && ExtraCombatInputGate;

    // Active and able to act - a dead Sentinel is still the active body but can't do anything.
    public virtual bool CanAct => IsActive;

    // Hook for a subclass with its own reason to suppress combat input beyond the checks above
    // (e.g. a Sentinel mid-token-placement, where attack/confirm_placement share the left-click
    // binding) - defaults to unrestricted so PlayerCharacter itself imposes no extra gate.
    protected virtual bool ExtraCombatInputGate => true;

    public virtual float Speed => BaseSpeed;

    public bool IsSprinting { get; private set; }

    // Hook for a subclass to end a sprint - a Sentinel firing, aiming, swinging, blocking or
    // rolling. It stays off until sprint is pressed again.
    protected virtual bool CanSprint => true;

    // Hook for a subclass to plant its feet - no walking, jumping or sprinting - while still letting
    // it look around and act. Forced movement like a dodge is the subclass's own to refuse.
    protected virtual bool CanMove => true;

    // Hook for a subclass to stop the camera turning while something else is reading the mouse or
    // stick.
    protected virtual bool CanLook => true;

    // While non-null, replaces input-driven horizontal movement for this tick - a dodge. Called once
    // per ApplyGroundMovement, so an override can advance its own timer here.
    protected virtual Vector3? UpdateForcedMovement(float delta) => null;

    // What the camera eases toward when not sprinting. Captured from the camera as authored, so a
    // subclass with its own FOV rules (Soldier's ADS) overrides this rather than writing Camera.Fov.
    protected virtual float BaseFov => _defaultFov;

    private float _defaultFov;

    // How far the camera sits off-center, as authored; which side is the player's setting.
    private float _shoulderOffset;

    // Subclasses read activity through IsActive and adjust look sensitivity through
    // LookSensitivity; nothing outside this class needs the backing fields.
    protected float LookSensitivity { get; set; } = 1f;

    protected LevelContext Level { get; private set; }

    private float _cameraPitch;
    private float _gravity;
    private bool _active;
    private bool _sprintLatched;

    // A jump press that reached gameplay input, spent by the next ground-movement tick. Read from
    // events rather than polled, so the press a UI panel handled - the one that closed it - can't
    // also jump.
    private bool _jumpQueued;
    private int _airJumpsLeft;

    public override void _Ready()
    {
        Level = LevelContext.Current;
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        _defaultFov = Camera.Fov;
        _shoulderOffset = Mathf.Abs(SpringArm.Position.X);

        // Another player's body is a puppet here: its owner's machine moves it and its
        // MultiplayerSynchronizer carries that across, so nothing on this side reads input or
        // simulates it.
        if (!IsLocallyControlled)
        {
            ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    public override void _ExitTree()
    {
        ReleaseActiveSlot();
    }

    public override void _Process(double delta)
    {
        float shoulderX = DebugSettings.CameraOnLeftShoulder ? -_shoulderOffset : _shoulderOffset;

        if (SpringArm.Position.X != shoulderX)
        {
            SpringArm.Position = SpringArm.Position with { X = shoulderX };
        }

        if (!IsActive || UiPanelStack.AnyOpen || !CanLook)
        {
            return;
        }

        Vector2 stick = Input.GetVector("look_left", "look_right", "look_up", "look_down");

        if (stick == Vector2.Zero)
        {
            return;
        }

        // Squared deflection: small tilts stay precise for aiming, full tilt still turns at full speed.
        float speed = Mathf.DegToRad(StickLookSpeedDegrees) * LookSensitivity * (float)delta;
        ApplyLook(stick * stick.Length() * speed);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsActive && !UiPanelStack.AnyOpen && @event.IsActionPressed("jump"))
        {
            _jumpQueued = true;
        }
    }

    public Vector3 GetCameraAimPoint(float distance)
    {
        Vector3 origin = Camera.GlobalPosition;
        Vector3 forward = -Camera.GlobalTransform.Basis.Z;
        float cameraSetback = origin.DistanceTo(GlobalPosition);
        return origin + forward * (distance + cameraSetback);
    }

    public void SetOwnerId(PlayerId owner) => OwnerId = owner;

    // Takes control of this body - its spawner calls this once the body is in place.
    public virtual void Activate()
    {
        SetActive(true);
    }

    // Where a body stands and looks - what a handoff carries across. A value rather than a body, so
    // a handoff can outlive the body it was read from.
    public readonly record struct Pose(Vector3 Position, float Yaw, float Pitch);

    public Pose CurrentPose => new Pose(GlobalPosition, GlobalRotation.Y, _cameraPitch);

    // For a body being retired: stops it sending sync updates while it waits to be removed, so none
    // arrive for it after other machines have already removed it. Only this body's owner sends them.
    public void StopSyncing()
    {
        if (IsLocallyControlled)
        {
            GetNodeOrNull("MultiplayerSynchronizer")?.QueueFree();
        }
    }

    public void ApplyPose(Pose pose)
    {
        GlobalPosition = pose.Position;
        GlobalRotation = new Vector3(0f, pose.Yaw, 0f);
        _cameraPitch = pose.Pitch;
        SpringArm.Rotation = new Vector3(_cameraPitch, 0f, 0f);
    }

    protected virtual void SetActive(bool value)
    {
        _active = value;
        _sprintLatched = false;
        _jumpQueued = false;
        BodyMesh.Visible = _active;

        if (_active)
        {
            Level.ActiveCharacters[OwnerId] = this;

            if (IsLocallyControlled)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
                MakeCameraCurrent();
            }
        }
        else
        {
            ReleaseActiveSlot();
        }
    }

    // Only ever removes this character's own entry: during a body swap the incoming body can claim
    // the slot before the outgoing one leaves.
    private void ReleaseActiveSlot()
    {
        if (Level.ActiveCharacters.TryGetValue(OwnerId, out PlayerCharacter current) && current == this)
        {
            Level.ActiveCharacters.Remove(OwnerId);
        }
    }

    private async void MakeCameraCurrent()
    {
        // SpringArm3D reports a hit length of 0 until it has run its first collision pass, and how
        // many frames that takes depends on where in the physics/process interleave this character
        // was spawned - one frame sometimes, three others. Going current before then renders the
        // camera collapsed onto the character, which is the zoomed-in flicker on a hub swap.
        // Bounded so a camera legitimately flush against geometry can't stall activation forever.
        for (int frame = 0; frame < 10 && SpringArm.GetHitLength() <= 0f; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            if (!IsInstanceValid(this) || !_active)
            {
                return;
            }
        }

        if (_active)
        {
            Camera.Current = true;
        }
    }

    protected void HandleLook(InputEventMouseMotion mouseMotion)
    {
        // A panel can keep the mouse captured (a radial wheel reading it as a stick), so being
        // captured alone doesn't mean the mouse belongs to the camera.
        if (Input.MouseMode != Input.MouseModeEnum.Captured || UiPanelStack.AnyOpen || !CanLook)
        {
            return;
        }

        ApplyLook(mouseMotion.Relative * MouseSensitivity * LookSensitivity);
    }

    private void ApplyLook(Vector2 radians)
    {
        RotateY(-radians.X);

        _cameraPitch = Mathf.Clamp(
            _cameraPitch - radians.Y,
            Mathf.DegToRad(MinPitchDegrees),
            Mathf.DegToRad(MaxPitchDegrees));

        SpringArm.Rotation = new Vector3(_cameraPitch, 0f, 0f);
    }

    protected void ApplyGroundMovement(double delta, Vector2 inputDir, bool allowJump = true)
    {
        bool jumpPressed = _jumpQueued;
        _jumpQueued = false;

        // A controller's sticks drive an open panel's focus instead of the character, a keyboard
        // typing into a text field isn't walking, and some panels stand the character still outright.
        if (!CanMove || (UiPanelStack.AnyOpen && InputDevice.UsingController) || UiPanelStack.IsTyping || UiPanelStack.BlocksMovement)
        {
            inputDir = Vector2.Zero;
            allowJump = false;
        }

        Vector3 velocity = Velocity;
        Vector3? forced = UpdateForcedMovement((float)delta);

        bool jumping = allowJump && forced == null && jumpPressed && !InputChords.AbilityModifierHeld;

        if (IsOnFloor())
        {
            _airJumpsLeft = AirJumps;

            if (jumping)
            {
                velocity.Y = JumpVelocity;
            }
        }
        else
        {
            velocity.Y -= _gravity * (float)delta;

            if (jumping && _airJumpsLeft > 0)
            {
                velocity.Y = JumpVelocity;
                _airJumpsLeft--;
            }
        }

        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0f, inputDir.Y)).Normalized();
        UpdateSprintLatch(inputDir);
        IsSprinting = forced == null && _sprintLatched;
        float speed = IsSprinting ? Speed * SprintSpeedMultiplier : Speed;

        if (forced.HasValue)
        {
            velocity.X = forced.Value.X;
            velocity.Z = forced.Value.Z;
        }
        else if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * speed;
            velocity.Z = direction.Z * speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0f, Speed);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0f, Speed);
        }

        Velocity = velocity;
        StepClimber.TryStepUp(this, velocity, (float)delta, StepHeight);
        MoveAndSlide();
        UpdateFov((float)delta);
    }

    // Sprint stays on once pressed rather than being held, so a controller player isn't pinning a
    // stick click down while steering with that same stick.
    private void UpdateSprintLatch(Vector2 inputDir)
    {
        if (Input.IsActionJustPressed("sprint"))
        {
            _sprintLatched = true;
        }

        bool movingBackward = inputDir.Y > Mathf.Abs(inputDir.X);

        if (inputDir == Vector2.Zero || movingBackward || !CanSprint)
        {
            _sprintLatched = false;
        }
    }

    private void UpdateFov(float delta)
    {
        float target = BaseFov + (IsSprinting ? SprintFovBonus : 0f);
        Camera.Fov = Mathf.Lerp(Camera.Fov, target, Mathf.Min(1f, delta * FovTransitionSpeed));
    }
}
