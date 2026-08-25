using Godot;

public partial class Soldier : PlayerCharacter
{
    [Export] public PlayerCharacter Architect { get; private set; }

    public override void _Ready()
    {
        base._Ready();
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        SetActive(false);
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
    }

    private void OnGamePhaseChanged(WaveController.GamePhase phase)
    {
        bool shouldBeActive = phase == WaveController.GamePhase.Combat;

        if (shouldBeActive)
        {
            HandoffFrom(Architect);
        }

        SetActive(shouldBeActive);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!active)
        {
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleLook(mouseMotion);
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            ToggleMouseCapture();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!active)
        {
            return;
        }

        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        ApplyGroundMovement(delta, inputDir);
    }
}
