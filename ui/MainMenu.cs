using Godot;
using System;
using System.Collections.Generic;

// The first screen - Play / Settings / Exit - and the profile screens Play leads to. Part of the
// persistent main scene rather than a level, so the level spawner never replicates it: it shows only
// while no level is loaded, and Play loads the hub. Launch flags skip it (LaunchFlags).
public partial class MainMenu : Control
{
    public static MainMenu Instance { get; private set; }

    private const string Title = "Runewarder";
    private const int MaxProfileNameLength = 24;

    private CenterContainer _screen;
    private OptionsMenu _options;
    private Button _settingsButton;

    // What Escape / Circle does on the current screen - null on the main screen, which has nowhere
    // to go back to.
    private Action _back;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        Visible = false;

        ColorRect background = new ColorRect { Color = new Color(0.08f, 0.08f, 0.1f) };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        _screen = new CenterContainer();
        _screen.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_screen);

        _options = new OptionsMenu();
        _options.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_options);
        _options.Closed += OnOptionsClosed;

        // Deferred until the whole main scene is ready - LevelLoader sets up its spawner in its own
        // _Ready, which runs after this one.
        CallDeferred(LaunchFlags.SkipsMenu ? MethodName.StartFromLaunchFlags : MethodName.Open);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && _back != null && UiPanelStack.IsTop(this) && @event.IsActionPressed("ui_cancel"))
        {
            _back();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Open()
    {
        Visible = true;
        // The menu can't be closed, only left through Play or Exit.
        UiPanelStack.Push(this, () => { });
        ShowMain();
    }

    // From the pause menu. Leaving the session drops the level with it.
    public void Return()
    {
        GetTree().Paused = false;
        SaveManager.Instance.FlushPendingSave();
        NetworkSession.Instance.LeaveSession(Open);
    }

    // Development only. Without --profile, the last-played profile, or a new one if there are none -
    // so two instances on one PC need different --profile numbers or they share a save.
    private void StartFromLaunchFlags()
    {
        SaveManager saves = SaveManager.Instance;
        bool selected = LaunchFlags.Profile is int id
            ? saves.SelectOrCreateProfile(id)
            : saves.SelectProfile(saves.LastPlayedProfileId ?? saves.CreateProfile("Profile 1"));

        if (!selected)
        {
            GD.PushWarning("MainMenu: the launch flags' profile couldn't be read - showing the menu instead.");
            Open();
            return;
        }

        // A joiner's hub is the host's, arriving through LevelLoader's spawner.
        if (LaunchFlags.JoinAddress != null)
        {
            NetworkSession.Instance.Join(LaunchFlags.JoinAddress);
            return;
        }

        if (LaunchFlags.Host)
        {
            NetworkSession.Instance.Host();
        }

        LevelLoader.Instance.LoadHub();
    }

    private void ShowMain()
    {
        VBoxContainer column = NewScreen(Title, back: null);
        Button play = AddButton(column, "Play", OnPlayPressed);
        _settingsButton = AddButton(column, "Settings", OpenOptions);
        AddButton(column, "Exit", () => GetTree().Quit());
        play.GrabFocus();
    }

    private void OpenOptions()
    {
        _screen.Visible = false;
        _options.Open();
    }

    private void OnOptionsClosed()
    {
        _screen.Visible = true;
        _settingsButton.GrabFocus();
    }

    // Straight in on the only profile, the picker when there are several, a new one when there are
    // none. A lone profile that can't be read goes to the picker, which can restore it.
    private void OnPlayPressed()
    {
        List<ProfileSummary> profiles = SaveManager.Instance.ListProfiles();

        if (profiles.Count == 0)
        {
            ShowNewProfile();
        }
        else if (profiles.Count == 1 && profiles[0].IsReadable)
        {
            Play(profiles[0].Id);
        }
        else
        {
            ShowPicker();
        }
    }

    private void Play(int profileId)
    {
        if (!SaveManager.Instance.SelectProfile(profileId))
        {
            ShowPicker();
            return;
        }

        Visible = false;
        UiPanelStack.Pop(this);
        LevelLoader.Instance.LoadHub();
    }

    private void ShowPicker()
    {
        VBoxContainer column = NewScreen("Choose a profile", ShowMain);

        // Twenty rows don't fit on screen; follow_focus keeps a controller's selection in view.
        ScrollContainer scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0f, 800f),
            FollowFocus = true,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        column.AddChild(scroll);

        VBoxContainer rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(rows);

        Button first = null;

        foreach (ProfileSummary profile in SaveManager.Instance.ListProfiles())
        {
            HBoxContainer row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            rows.AddChild(row);

            if (profile.IsReadable)
            {
                Button play = AddButton(row, $"{profile.Name}    Gold: {profile.Gold}    {LastPlayedText(profile)}", () => Play(profile.Id), 720f);
                first ??= play;
            }
            else
            {
                AddButton(row, $"{profile.Name} - can't be read", null, 720f).Disabled = true;

                if (profile.HasBackup)
                {
                    Button restore = AddButton(row, "Restore", () => Restore(profile.Id), 160f);
                    first ??= restore;
                }
            }

            Button delete = AddButton(row, "Delete", () => ShowDeleteConfirm(profile), 160f);
            first ??= delete;
        }

        bool canCreate = SaveManager.Instance.CanCreateProfile;
        Button create = AddButton(column, canCreate ? "New Profile" : $"New Profile ({SaveManager.MaxProfiles} max)", ShowNewProfile);
        create.Disabled = !canCreate;
        Button back = AddButton(column, "Back", ShowMain);

        (first ?? (canCreate ? create : back)).GrabFocus();
    }

    private static string LastPlayedText(ProfileSummary profile) =>
        profile.LastPlayedUnix == 0
            ? "Never played"
            : $"Last played {DateTimeOffset.FromUnixTimeSeconds(profile.LastPlayedUnix).ToLocalTime():MMM d, yyyy}";

    // Back to the picker either way - a restored profile shows as playable again.
    private void Restore(int profileId)
    {
        SaveManager.Instance.RestoreBackup(profileId);
        ShowPicker();
    }

    private void ShowDeleteConfirm(ProfileSummary profile)
    {
        VBoxContainer column = NewScreen($"Delete \"{profile.Name}\"?", ShowPicker);
        column.AddChild(new Label { Text = "It goes to the recycle bin, along with its backups.", HorizontalAlignment = HorizontalAlignment.Center });

        AddButton(column, "Delete", () =>
        {
            SaveManager.Instance.DeleteProfile(profile.Id);
            ShowPicker();
        });

        // Cancel takes focus, so a stray confirm press deletes nothing.
        AddButton(column, "Cancel", ShowPicker).GrabFocus();
    }

    private void ShowNewProfile()
    {
        int existing = SaveManager.Instance.ListProfiles().Count;
        Action back = existing == 0 ? ShowMain : ShowPicker;
        string suggested = $"Profile {existing + 1}";
        VBoxContainer column = NewScreen("New profile", back);

        LineEdit name = new LineEdit
        {
            Text = suggested,
            MaxLength = MaxProfileNameLength,
            SelectAllOnFocus = true,
            CustomMinimumSize = new Vector2(320f, 56f)
        };
        column.AddChild(name);

        name.TextSubmitted += _ => Create(name.Text, suggested);
        Button create = AddButton(column, "Create", () => Create(name.Text, suggested));
        AddButton(column, "Cancel", back);

        // A controller has nothing to type with yet, so it lands on Create with the suggested name.
        if (InputDevice.UsingController)
        {
            create.GrabFocus();
        }
        else
        {
            name.GrabFocus();
        }
    }

    private void Create(string name, string suggested)
    {
        string trimmed = name.StripEdges();
        Play(SaveManager.Instance.CreateProfile(trimmed == "" ? suggested : trimmed));
    }

    // Clears the current screen and starts the next under its heading. back is what Escape / Circle
    // does there.
    private VBoxContainer NewScreen(string heading, Action back)
    {
        foreach (Node child in _screen.GetChildren())
        {
            child.QueueFree();
        }

        VBoxContainer column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        _screen.AddChild(column);

        Label label = new Label { Text = heading, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 28);
        column.AddChild(label);

        _back = back;
        return column;
    }

    private static Button AddButton(Container parent, string text, Action onPressed, float width = 320f)
    {
        Button button = new Button { Text = text, CustomMinimumSize = new Vector2(width, 56f) };
        button.Pressed += onPressed;
        parent.AddChild(button);
        return button;
    }
}
