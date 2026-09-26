using Godot;

public partial class Architect : PlayerCharacter
{
    [Export] public BuildMenu BuildMenu { get; private set; }
    [Export] public RitualPlacer RitualPlacer { get; private set; }

    private bool _buildMenuOpen;

    public override void _Ready()
    {
        base._Ready();
        Level.Architects[OwnerId] = this;
        BuildMenu.Closed += OnBuildMenuClosed;
        BuildMenu.Confirmed += OnBuildMenuConfirmed;
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;

        // Spawned into a match already underway, the phase events so far were missed - catch up
        // on the current phase. The hub has no match; its spawner activates the body directly.
        if (Level.Match != null)
        {
            OnGamePhaseChanged(Level.Match.Phase);
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;

        if (Level.Architects.TryGetValue(OwnerId, out Architect current) && current == this)
        {
            Level.Architects.Remove(OwnerId);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_buildMenuOpen || !IsActive)
        {
            return;
        }

        if (@event.IsActionPressed("open_build_menu") && !RitualPlacer.IsPlacing)
        {
            OpenBuildMenu();
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleLook(mouseMotion);
        }

        if (@event.IsActionPressed("ui_cancel") && !RitualPlacer.IsPlacing && @event is not InputEventJoypadButton)
        {
            ToggleMouseCapture();
        }
    }

    private void OpenBuildMenu()
    {
        _buildMenuOpen = true;
        BuildMenu.Open();
    }

    private void OnBuildMenuClosed()
    {
        _buildMenuOpen = false;
    }

    private void OnBuildMenuConfirmed(Spell spell, Godot.Collections.Array<Rune> runes)
    {
        RitualPlacer.BeginPlacement(spell, runes);
    }

    private void OnGamePhaseChanged(GamePhase phase)
    {
        bool shouldBeActive = phase == GamePhase.Build;

        if (shouldBeActive)
        {
            HandoffFrom(Level.Sentinels[OwnerId]);
        }

        SetActive(shouldBeActive);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsActive)
        {
            return;
        }

        Vector2 inputDir = _buildMenuOpen ? Vector2.Zero : Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        ApplyGroundMovement(delta, inputDir, allowJump: !_buildMenuOpen);
    }
}
