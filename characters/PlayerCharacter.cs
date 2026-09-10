using Godot;

public abstract partial class PlayerCharacter : CharacterBody3D
{
    // Whichever PlayerCharacter subclass is currently active is the valid enemy-attack target.
    // Architect and Sentinel are never simultaneously active (each phase transition deactivates
    // one and activates the other), and no enemies exist during Build phase to read this while
    // it points at Architect — so no subclass needs to opt in or out individually.
    public static PlayerCharacter ActiveCombatant { get; private set; }

    [Export] public PlayerId OwnerId { get; private set; } = PlayerId.P1;
    [Export] public float BaseSpeed = 5.0f;
    [Export] public float JumpVelocity = 4.5f;
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float MinPitchDegrees = -60f;
    [Export] public float MaxPitchDegrees = 10f;
    [Export] public SpringArm3D SpringArm { get; private set; }
    [Export] public Camera3D Camera { get; private set; }
    [Export] public MeshInstance3D BodyMesh { get; private set; }

    public bool IsActive => _active;

    // True only while this character is both the active combatant and no UI panel (inventory,
    // build menu, etc.) is capturing input instead - combat actions check this rather than IsActive
    // alone, so clicking inside an open panel doesn't also swing a sword or fire a gun.
    protected bool CanUseCombatInput => IsActive && !UiPanelStack.AnyOpen && ExtraCombatInputGate;

    // Hook for a subclass with its own reason to suppress combat input beyond the checks above
    // (e.g. a Sentinel mid-token-placement, where attack/confirm_placement share the left-click
    // binding) - defaults to unrestricted so PlayerCharacter itself imposes no extra gate.
    protected virtual bool ExtraCombatInputGate => true;

    public virtual float Speed => BaseSpeed;

    // Subclasses read activity through IsActive and adjust look sensitivity through
    // LookSensitivity; nothing outside this class needs the backing fields.
    protected float LookSensitivity { get; set; } = 1f;

    private float _cameraPitch;
    private float _gravity;
    private bool _active;

    public override void _Ready()
    {
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
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
            ActiveCombatant = this;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            MakeCameraCurrent();
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

        float sensitivity = MouseSensitivity * LookSensitivity;

        RotateY(-mouseMotion.Relative.X * sensitivity);

        _cameraPitch = Mathf.Clamp(
            _cameraPitch - mouseMotion.Relative.Y * sensitivity,
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
        Vector3 velocity = Velocity;

        if (!IsOnFloor())
        {
            velocity.Y -= _gravity * (float)delta;
        }
        else if (allowJump && Input.IsActionJustPressed("jump"))
        {
            velocity.Y = JumpVelocity;
        }

        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0f, inputDir.Y)).Normalized();

        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * Speed;
            velocity.Z = direction.Z * Speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0f, Speed);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0f, Speed);
        }

        Velocity = velocity;
        MoveAndSlide();
    }
}
