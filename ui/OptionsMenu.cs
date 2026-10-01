using Godot;
using System;
using System.Collections.Generic;

// The pause menu's settings screen - the DebugSettings switches and the VideoSettings options, each
// saved the moment it changes. Opens over the pause menu and backs out to it; process_mode Always (player_hud.tscn),
// like the menu that opens it.
public partial class OptionsMenu : Control
{
    public event Action Closed;

    private CheckBox _firstOption;

    public override void _Ready()
    {
        Visible = false;

        CenterContainer center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        VBoxContainer column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        center.AddChild(column);

        Label title = new Label { Text = "Options", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        column.AddChild(title);

        _firstOption = AddToggle(column, "Enemy health bars", DebugSettings.ShowEnemyHealthBars, on =>
        {
            DebugSettings.ShowEnemyHealthBars = on;
            RefreshLiveHealthBars();
        }, DebugSettings.Save);
        AddToggle(column, "Weapon damage numbers", DebugSettings.ShowWeaponDamageNumbers,
            on => DebugSettings.ShowWeaponDamageNumbers = on, DebugSettings.Save);
        AddToggle(column, "Ritual damage numbers", DebugSettings.ShowRitualDamageNumbers,
            on => DebugSettings.ShowRitualDamageNumbers = on, DebugSettings.Save);

        Label video = new Label { Text = "Video", HorizontalAlignment = HorizontalAlignment.Center };
        video.AddThemeFontSizeOverride("font_size", 20);
        column.AddChild(video);

        AddResolutionPicker(column);
        AddVideoToggle(column, "Glow", VideoSettings.Glow, on => VideoSettings.Glow = on);
        AddVideoToggle(column, "Ambient occlusion", VideoSettings.AmbientOcclusion, on => VideoSettings.AmbientOcclusion = on);
        AddVideoToggle(column, "Shadows", VideoSettings.Shadows, on => VideoSettings.Shadows = on);

        Button back = new Button { Text = "Back", CustomMinimumSize = new Vector2(320f, 56f) };
        back.Pressed += Close;
        column.AddChild(back);
    }

    private static CheckBox AddToggle(Container column, string text, bool initial, Action<bool> onToggled, Action save)
    {
        CheckBox toggle = new CheckBox { Text = text, ButtonPressed = initial };
        toggle.Toggled += pressed =>
        {
            onToggled(pressed);
            save();
        };
        column.AddChild(toggle);
        return toggle;
    }

    private static void AddVideoToggle(Container column, string text, bool initial, Action<bool> set)
    {
        AddToggle(column, text, initial, on =>
        {
            set(on);
            ApplyVideo();
        }, VideoSettings.Save);
    }

    private static void AddResolutionPicker(Container column)
    {
        HBoxContainer row = new HBoxContainer();
        row.AddChild(new Label { Text = "Resolution", SizeFlagsHorizontal = SizeFlags.ExpandFill });

        OptionButton picker = new OptionButton();
        List<int> heights = VideoSettings.ResolutionChoices();

        for (int i = 0; i < heights.Count; i++)
        {
            Vector2I size = VideoSettings.SizeFor(heights[i]);
            picker.AddItem(i == 0 ? $"{size.X} x {size.Y} (native)" : $"{size.X} x {size.Y}");
        }

        // A saved height this window doesn't offer (a different screen) falls back to native.
        picker.Selected = Math.Max(0, heights.IndexOf(VideoSettings.ResolutionHeight));
        picker.ItemSelected += index =>
        {
            VideoSettings.ResolutionHeight = index == 0 ? 0 : heights[(int)index];
            VideoSettings.Save();
            ApplyVideo();
        };

        row.AddChild(picker);
        column.AddChild(row);
    }

    // Opened from the main menu there's no level yet; the next one to load applies them.
    private static void ApplyVideo()
    {
        if (LevelContext.Current != null)
        {
            VideoSettings.Apply(LevelContext.Current);
        }
    }

    // Enemies already in the level read the setting once, on spawn.
    private static void RefreshLiveHealthBars()
    {
        if (LevelContext.Current == null)
        {
            return;
        }

        foreach (Enemy enemy in LevelContext.Current.Enemies.Active)
        {
            enemy.RefreshHealthBarVisibility();
        }
    }

    public void Open()
    {
        Visible = true;
        UiPanelStack.Push(this, Close);
        _firstOption.GrabFocus();
    }

    private void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        UiPanelStack.Pop(this);
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && UiPanelStack.IsTop(this) && @event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
}
