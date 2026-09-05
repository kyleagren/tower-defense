using Godot;
using System.Collections.Generic;

public partial class Architect : PlayerCharacter
{
    public static Dictionary<PlayerId, Architect> ByOwner { get; } = new();

    [Export] public BuildMenu BuildMenu { get; private set; }
    [Export] public RitualPlacer RitualPlacer { get; private set; }

    private bool _buildMenuOpen;

    public override void _Ready()
    {
        base._Ready();
        ByOwner[OwnerId] = this;
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

        if (@event.IsActionPressed("ui_cancel") && !RitualPlacer.IsPlacing)
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
            HandoffFrom(Sentinel.ByOwner[OwnerId]);
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
