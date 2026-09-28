using Godot;

// Resume / Options / Return to Hub (host, in a match) or Leave Session (anyone who joined) / Main
// Menu / Exit Game. Opened by the pause action, or by Escape when nothing else wanted it. Freezes the game while
// open, unless other players share the session - one player's menu can't stop everyone else's game.
// Runs with process_mode Always (player_hud.tscn) so it keeps working while the tree it paused is
// stopped.
public partial class PauseMenu : Control
{
    public static PauseMenu Instance { get; private set; }

    [Export] public OptionsMenu Options { get; private set; }

    private VBoxContainer _column;
    private Button _resumeButton;
    private Button _optionsButton;
    private Button _returnToHubButton;
    private Button _leaveSessionButton;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        NetworkSession.Instance.SeatsChanged -= OnSeatsChanged;
    }

    public override void _Ready()
    {
        Visible = false;

        ColorRect dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f) };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        CenterContainer center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _column = new VBoxContainer();
        _column.AddThemeConstantOverride("separation", 12);
        center.AddChild(_column);

        _resumeButton = AddButton(_column, "Resume", Close);
        _optionsButton = AddButton(_column, "Options", OpenOptions);
        _returnToHubButton = AddButton(_column, "Return to Hub", ReturnToHub);
        _leaveSessionButton = AddButton(_column, "Leave Session", LeaveSession);
        AddButton(_column, "Main Menu", ReturnToMainMenu);
        AddButton(_column, "Exit Game", () => GetTree().Quit());

        Options.Closed += OnOptionsClosed;
        NetworkSession.Instance.SeatsChanged += OnSeatsChanged;
    }

    private static Button AddButton(Container column, string text, System.Action onPressed)
    {
        Button button = new Button { Text = text, CustomMinimumSize = new Vector2(320f, 56f) };
        button.Pressed += onPressed;
        column.AddChild(button);
        return button;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pause"))
        {
            if (Visible)
            {
                UiPanelStack.CloseAll();
            }
            else
            {
                Open();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && UiPanelStack.IsTop(this) && @event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // Opens alone - anything already open closes first, the same as Escape closing the inventory
    // rather than pausing over it.
    public void Open()
    {
        if (Visible)
        {
            return;
        }

        UiPanelStack.CloseAll();
        bool isHost = NetworkSession.Instance.IsHost;
        _returnToHubButton.Visible = isHost && LevelContext.Current?.Match != null;
        _leaveSessionButton.Visible = !isHost;
        Visible = true;
        UiPanelStack.Push(this, Close);
        GetTree().Paused = !PlayerProfiles.IsMultiplayer;
        _resumeButton.GrabFocus();
    }

    private void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        UiPanelStack.Pop(this);
        GetTree().Paused = false;
    }

    private void OnSeatsChanged()
    {
        if (Visible)
        {
            GetTree().Paused = !PlayerProfiles.IsMultiplayer;
        }
    }

    private void OpenOptions()
    {
        _column.Visible = false;
        Options.Open();
    }

    // Also fires when a pause toggle closes both at once, by which point this menu is already gone.
    private void OnOptionsClosed()
    {
        _column.Visible = true;

        if (Visible)
        {
            _optionsButton.GrabFocus();
        }
    }

    private void LeaveSession()
    {
        UiPanelStack.CloseAll();
        NetworkSession.Instance.LeaveSession(LevelLoader.Instance.LoadHub);
    }

    // Leaves any session - for a host, that ends it for everyone else too.
    private void ReturnToMainMenu()
    {
        UiPanelStack.CloseAll();
        MainMenu.Instance.Return();
    }

    // Leaving mid-match is the level's to carry out - GameOverHandler already owns the way back to
    // the hub, so this menu never needs to know where that is.
    private void ReturnToHub()
    {
        GetTree().Paused = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.ReturnToHubRequested);
    }
}
