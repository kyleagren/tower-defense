using Godot;

public partial class Architect : CharacterBody3D
{
    [Export] public float Speed = 5.0f;
    [Export] public float JumpVelocity = 4.5f;
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float MinPitchDegrees = -60f;
    [Export] public float MaxPitchDegrees = 10f;
    [Export] public SpringArm3D SpringArm { get; private set; }

    private float cameraPitch;
    private float gravity;

    public override void _Ready()
    {
        gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);

            cameraPitch = Mathf.Clamp(
                cameraPitch - mouseMotion.Relative.Y * MouseSensitivity,
                Mathf.DegToRad(MinPitchDegrees),
                Mathf.DegToRad(MaxPitchDegrees));

            SpringArm.Rotation = new Vector3(cameraPitch, 0f, 0f);
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 velocity = Velocity;

        if (!IsOnFloor())
        {
            velocity.Y -= gravity * (float)delta;
        }
        else if (Input.IsActionJustPressed("jump"))
        {
            velocity.Y = JumpVelocity;
        }

        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
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
