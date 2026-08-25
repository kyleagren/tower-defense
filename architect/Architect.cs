using Godot;

public partial class Architect : CharacterBody3D
{
    [Export] public float Speed = 5.0f;
    [Export] public float JumpVelocity = 4.5f;
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float MinPitchDegrees = -60f;
    [Export] public float MaxPitchDegrees = 10f;
    [Export] public SpringArm3D SpringArm { get; private set; }
    [Export] public BuildMenu BuildMenu { get; private set; }
    [Export] public TowerPlacer TowerPlacer { get; private set; }

    private float cameraPitch;
    private float gravity;
    private bool buildMenuOpen;

    public override void _Ready()
    {
        gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        Input.MouseMode = Input.MouseModeEnum.Captured;
        BuildMenu.Closed += OnBuildMenuClosed;
        BuildMenu.Confirmed += OnBuildMenuConfirmed;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (buildMenuOpen)
        {
            return;
        }

        if (@event.IsActionPressed("open_build_menu") && !TowerPlacer.IsPlacing)
        {
            OpenBuildMenu();
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);

            cameraPitch = Mathf.Clamp(
                cameraPitch - mouseMotion.Relative.Y * MouseSensitivity,
                Mathf.DegToRad(MinPitchDegrees),
                Mathf.DegToRad(MaxPitchDegrees));

            SpringArm.Rotation = new Vector3(cameraPitch, 0f, 0f);
        }

        if (@event.IsActionPressed("ui_cancel") && !TowerPlacer.IsPlacing)
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    private void OpenBuildMenu()
    {
        buildMenuOpen = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        BuildMenu.Open();
    }

    private void OnBuildMenuClosed()
    {
        buildMenuOpen = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void OnBuildMenuConfirmed(Spell spell, Godot.Collections.Array<Rune> runes)
    {
        TowerPlacer.BeginPlacement(spell, runes);
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 velocity = Velocity;

        if (!IsOnFloor())
        {
            velocity.Y -= gravity * (float)delta;
        }
        else if (!buildMenuOpen && Input.IsActionJustPressed("jump"))
        {
            velocity.Y = JumpVelocity;
        }

        Vector2 inputDir = buildMenuOpen ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_back");
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
