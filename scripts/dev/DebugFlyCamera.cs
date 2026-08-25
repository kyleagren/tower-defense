using Godot;

public partial class DebugFlyCamera : Camera3D
{
    [Export] public float MoveSpeed = 10f;
    [Export] public float FastMoveMultiplier = 3f;
    [Export] public float MouseSensitivity = 0.003f;

    private bool mouseCaptured;
    private bool active;

    public override void _Ready()
    {
        EventBus.Instance.WavePhaseChanged += OnWavePhaseChanged;
    }

    private void OnWavePhaseChanged(WaveController.WavePhase phase)
    {
        active = phase == WaveController.WavePhase.Wave;

        if (active)
        {
            Current = true;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!active)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Right)
        {
            mouseCaptured = mouseButton.Pressed;
            Input.MouseMode = mouseCaptured ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        }

        if (mouseCaptured && @event is InputEventMouseMotion mouseMotion)
        {
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);
            RotateObjectLocal(Vector3.Right, -mouseMotion.Relative.Y * MouseSensitivity);
        }
    }

    public override void _Process(double delta)
    {
        if (!active)
        {
            return;
        }

        Vector3 direction = Vector3.Zero;

        if (Input.IsKeyPressed(Key.W)) direction -= Transform.Basis.Z;
        if (Input.IsKeyPressed(Key.S)) direction += Transform.Basis.Z;
        if (Input.IsKeyPressed(Key.A)) direction -= Transform.Basis.X;
        if (Input.IsKeyPressed(Key.D)) direction += Transform.Basis.X;
        if (Input.IsKeyPressed(Key.E)) direction += Transform.Basis.Y;
        if (Input.IsKeyPressed(Key.Q)) direction -= Transform.Basis.Y;

        if (direction != Vector3.Zero)
        {
            float speed = MoveSpeed * (Input.IsKeyPressed(Key.Shift) ? FastMoveMultiplier : 1f);
            GlobalPosition += direction.Normalized() * speed * (float)delta;
        }
    }
}
