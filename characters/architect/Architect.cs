using Godot;

public partial class Architect : PlayerCharacter
{
    [Export] public BuildMenu BuildMenu { get; private set; }
    [Export] public TowerPlacer TowerPlacer { get; private set; }
    [Export] public PlayerCharacter Soldier { get; private set; }

    private bool buildMenuOpen;

    public override void _Ready()
    {
        base._Ready();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        BuildMenu.Closed += OnBuildMenuClosed;
        BuildMenu.Confirmed += OnBuildMenuConfirmed;
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        SetActive(true);
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (buildMenuOpen || !active)
        {
            return;
        }

        if (@event.IsActionPressed("open_build_menu") && !TowerPlacer.IsPlacing)
        {
            OpenBuildMenu();
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleLook(mouseMotion);
        }

        if (@event.IsActionPressed("ui_cancel") && !TowerPlacer.IsPlacing)
        {
            ToggleMouseCapture();
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

    private void OnGamePhaseChanged(WaveController.GamePhase phase)
    {
        bool shouldBeActive = phase == WaveController.GamePhase.Build;

        if (shouldBeActive)
        {
            HandoffFrom(Soldier);
        }

        SetActive(shouldBeActive);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!active)
        {
            return;
        }

        Vector2 inputDir = buildMenuOpen ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        ApplyGroundMovement(delta, inputDir, allowJump: !buildMenuOpen);
    }
}
