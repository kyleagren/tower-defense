using Godot;
using System;

// The pause menu's settings screen - for now just the DebugSettings switches, each saved the moment
// it changes. Opens over the pause menu and backs out to it; process_mode Always (player_hud.tscn),
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
        });
        AddToggle(column, "Weapon damage numbers", DebugSettings.ShowWeaponDamageNumbers,
            on => DebugSettings.ShowWeaponDamageNumbers = on);
        AddToggle(column, "Ritual damage numbers", DebugSettings.ShowRitualDamageNumbers,
            on => DebugSettings.ShowRitualDamageNumbers = on);

        Button back = new Button { Text = "Back", CustomMinimumSize = new Vector2(320f, 56f) };
        back.Pressed += Close;
        column.AddChild(back);
    }

    private static CheckBox AddToggle(Container column, string text, bool initial, Action<bool> onToggled)
    {
        CheckBox toggle = new CheckBox { Text = text, ButtonPressed = initial };
        toggle.Toggled += pressed =>
        {
            onToggled(pressed);
            DebugSettings.Save();
        };
        column.AddChild(toggle);
        return toggle;
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
