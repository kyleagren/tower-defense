using Godot;

public abstract partial class PlayerCharacter : CharacterBody3D
{
    [Export] public PlayerId OwnerId { get; private set; } = PlayerId.P1;
    [Export] public float BaseSpeed = 5.0f;
    [Export] public float JumpVelocity = 4.5f;
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

    // Hook for a subclass to hold sprint off - a Sentinel mid-attack or mid-dodge. Holding the
    // sprint key through it resumes sprinting the moment it clears.
    protected virtual bool CanSprint => true;

    // Hook for a subclass to plant its feet - no walking, jumping or sprinting - while still letting
    // it look around and act. Forced movement like a dodge is the subclass's own to refuse.
    protected virtual bool CanMove => true;

    // While non-null, replaces input-driven horizontal movement for this tick - a dodge. Called once
    // per ApplyGroundMovement, so an override can advance its own timer here.
    protected virtual Vector3? UpdateForcedMovement(float delta) => null;

    // What the camera eases toward when not sprinting. Captured from the camera as authored, so a
    // subclass with its own FOV rules (Soldier's ADS) overrides this rather than writing Camera.Fov.
    protected virtual float BaseFov => _defaultFov;

    private float _defaultFov;

    // Subclasses read activity through IsActive and adjust look sensitivity through
    // LookSensitivity; nothing outside this class needs the backing fields.
    protected float LookSensitivity { get; set; } = 1f;

    protected LevelContext Level { get; private set; }

    private float _cameraPitch;
    private float _gravity;
    private bool _active;

    public override void _Ready()
    {
        Level = LevelContext.Current;
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        _defaultFov = Camera.Fov;
    }

    public override void _ExitTree()
    {
        ReleaseActiveSlot();
    }

    public override void _Process(double delta)
    {
        if (!IsActive || UiPanelStack.AnyOpen)
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

    public Vector3 GetCameraAimPoint(float distance)
    {
        Vector3 origin = Camera.GlobalPosition;
        Vector3 forward = -Camera.GlobalTransform.Basis.Z;
        float cameraSetback = origin.DistanceTo(GlobalPosition);
        return origin + forward * (distance + cameraSetback);
    }

    public void SetOwnerId(PlayerId owner) => OwnerId = owner;

    // Activates this character directly, without any phase system - for contexts like the hub,
    // which has no phase system and nothing to hand control from.
    public virtual void ActivateStandalone()
    {
        SetActive(true);
    }

    public void HandoffFrom(PlayerCharacter previous)
    {
        GlobalPosition = previous.GlobalPosition;
        GlobalRotation = new Vector3(0f, previous.GlobalRotation.Y, 0f);
        _cameraPitch = previous._cameraPitch;
        SpringArm.Rotation = new Vector3(_cameraPitch, 0f, 0f);
    }

    protected virtual void SetActive(bool value)
    {
        _active = value;
        BodyMesh.Visible = _active;

        if (_active)
        {
            Level.ActiveCharacters[OwnerId] = this;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            MakeCameraCurrent();
        }
        else
        {
            ReleaseActiveSlot();
        }
    }

    // Only ever removes this character's own entry: the Sentinel and Architect both react to the
    // same phase change in no guaranteed order, so the one activating may already have claimed it.
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
        if (Input.MouseMode != Input.MouseModeEnum.Captured)
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

    protected void ToggleMouseCapture()
    {
        Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Captured;
    }

    protected void ApplyGroundMovement(double delta, Vector2 inputDir, bool allowJump = true)
    {
        if (!CanMove)
        {
            inputDir = Vector2.Zero;
            allowJump = false;
        }

        Vector3 velocity = Velocity;
        Vector3? forced = UpdateForcedMovement((float)delta);

        if (!IsOnFloor())
        {
            velocity.Y -= _gravity * (float)delta;
        }
        else if (allowJump && forced == null && Input.IsActionJustPressed("jump"))
        {
            velocity.Y = JumpVelocity;
        }

        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0f, inputDir.Y)).Normalized();
        IsSprinting = forced == null && direction != Vector3.Zero && CanSprint && Input.IsActionPressed("sprint");
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
        MoveAndSlide();
        UpdateFov((float)delta);
    }

    private void UpdateFov(float delta)
    {
        float target = BaseFov + (IsSprinting ? SprintFovBonus : 0f);
        Camera.Fov = Mathf.Lerp(Camera.Fov, target, Mathf.Min(1f, delta * FovTransitionSpeed));
    }
}
