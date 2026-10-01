using Godot;

public partial class Architect : PlayerCharacter
{
    [Export] public BuildMenu BuildMenu { get; private set; }
    [Export] public RitualPlacer RitualPlacer { get; private set; }

    private static readonly string[] _tokenSlotActions = { "token_slot_1", "token_slot_2", "token_slot_3" };

    private bool _buildMenuOpen;

    public override void _Ready()
    {
        base._Ready();
        BuildMenu.Closed += OnBuildMenuClosed;
        BuildMenu.Confirmed += OnBuildMenuConfirmed;
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        base._UnhandledInput(@event);

        if (_buildMenuOpen || !IsActive)
        {
            return;
        }

        if (@event.IsActionPressed("open_build_menu") && !RitualPlacer.IsPlacing)
        {
            OpenBuildMenu();
            return;
        }

        // The Architect has no abilities, so 1/2/3 - or the d-pad with R1 held, as when deploying -
        // open the build menu straight into authoring that token.
        for (int token = 0; token < _tokenSlotActions.Length; token++)
        {
            if (@event.IsActionPressed(_tokenSlotActions[token]) && !RitualPlacer.IsPlacing
                && (@event is not InputEventJoypadButton || InputChords.TokenModifierHeld))
            {
                OpenBuildMenu(token);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleLook(mouseMotion);
        }

        if (@event.IsActionPressed("ui_cancel") && !RitualPlacer.IsPlacing && !UiPanelStack.AnyOpen
            && @event is not InputEventJoypadButton)
        {
            PauseMenu.Instance?.Open();
            GetViewport().SetInputAsHandled();
        }
    }

    // With a token, opens straight into authoring it.
    private void OpenBuildMenu(int token = -1)
    {
        _buildMenuOpen = true;
        BuildMenu.Open(token);
    }

    private void OnBuildMenuClosed()
    {
        _buildMenuOpen = false;
    }

    private void OnBuildMenuConfirmed(Spell spell, Godot.Collections.Array<Rune> runes)
    {
        RitualPlacer.BeginPlacement(spell, runes);
    }

    // Combat hands the player back to their Sentinel and this body is about to be swapped out, so
    // it stands down now.
    private void OnGamePhaseChanged(GamePhase phase)
    {
        if (phase != GamePhase.Build)
        {
            SetActive(false);
        }
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
