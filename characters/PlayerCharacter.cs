using Godot;

public abstract partial class PlayerCharacter : CharacterBody3D
{
    // Whichever PlayerCharacter subclass is currently active is the valid enemy-attack target.
    // Architect and Soldier are never simultaneously active (each phase transition deactivates
    // one and activates the other), and no enemies exist during Build phase to read this while
    // it points at Architect — so no subclass needs to opt in or out individually.
    public static PlayerCharacter ActiveCombatant { get; private set; }

    [Export] public float BaseSpeed = 5.0f;
    [Export] public float JumpVelocity = 4.5f;
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float MinPitchDegrees = -60f;
    [Export] public float MaxPitchDegrees = 10f;
    [Export] public SpringArm3D SpringArm { get; private set; }
    [Export] public Camera3D Camera { get; private set; }
    [Export] public MeshInstance3D BodyMesh { get; private set; }

    public bool IsActive => _active;

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
            Camera.Current = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
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
