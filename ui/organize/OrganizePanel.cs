using Godot;
using System;
using System.Collections.Generic;

// Where the player arranges the construction menu by hand, opened from the character sheet: one
// panel with a tab per thing arranged. Split by tab across OrganizePanel.SpellWheels.cs,
// OrganizePanel.Favorites.cs and OrganizePanel.Runes.cs; this file holds the frame, switching tabs,
// backing out and closing.
public partial class OrganizePanel : Control
{
    public enum Tab
    {
        SpellWheels,
        Favorites,
        Runes
    }

    public event Action Closed;

    private static readonly SpellWheelGroup[] _groups = Enum.GetValues<SpellWheelGroup>();

    private readonly Dictionary<Tab, Button> _tabButtons = new();
    private readonly Dictionary<Tab, Control> _pages = new();

    private static PlayerProfile Profile => PlayerProfiles.Local;

    public override void _Ready()
    {
        Visible = false;

        ColorRect dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f) };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        CenterContainer center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        PanelContainer frame = new PanelContainer { CustomMinimumSize = new Vector2(1800f, 1100f) };
        center.AddChild(frame);

        MarginContainer margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        frame.AddChild(margin);

        VBoxContainer column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        margin.AddChild(column);

        Label title = new Label { Text = "Organize" };
        title.AddThemeFontSizeOverride("font_size", 28);
        column.AddChild(title);

        HBoxContainer tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 8);
        column.AddChild(tabs);

        AddTab(tabs, Tab.SpellWheels, "Spell Wheels");
        AddTab(tabs, Tab.Favorites, "Favorites");
        AddTab(tabs, Tab.Runes, "Runes");

        _pages[Tab.SpellWheels] = BuildSpellWheelsPage();
        _pages[Tab.Favorites] = BuildFavoritesPage();
        _pages[Tab.Runes] = BuildRunesPage();

        foreach (Control page in _pages.Values)
        {
            column.AddChild(page);
        }

        column.AddChild(new Label
        {
            Text = "Press an entry to pick it up, move it up or down, and press again to put it down.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        Button back = new Button { Text = "Back", CustomMinimumSize = new Vector2(240f, 56f), SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        back.Pressed += Close;
        column.AddChild(back);
    }

    private void AddTab(Container tabs, Tab tab, string text)
    {
        Button button = new Button { Text = text, ToggleMode = true, CustomMinimumSize = new Vector2(240f, 56f) };
        button.Pressed += () => ShowTab(tab);
        tabs.AddChild(button);
        _tabButtons[tab] = button;
    }

    public void Open(Tab tab)
    {
        Visible = true;
        UiPanelStack.Push(this, Close);
        ShowTab(tab);
    }

    private void ShowTab(Tab tab)
    {
        CancelAllInProgress();

        foreach ((Tab each, Button button) in _tabButtons)
        {
            button.ButtonPressed = each == tab;
        }

        foreach ((Tab each, Control page) in _pages)
        {
            page.Visible = each == tab;
        }

        switch (tab)
        {
            case Tab.SpellWheels:
                ShowSpellWheelsPage();
                break;

            case Tab.Favorites:
                ShowFavoritesPage();
                FocusFavoritesStart();
                break;

            case Tab.Runes:
                ShowRunesPage();
                break;
        }
    }

    // Undoes one thing in progress - a held entry, a slot being chosen - and says whether there was one.
    private bool CancelInProgress() => CancelSpellWheelsInProgress() || CancelFavoritesInProgress() || CancelRunesInProgress();

    private void CancelAllInProgress()
    {
        while (CancelInProgress())
        {
        }
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        CancelAllInProgress();
        Visible = false;
        UiPanelStack.Pop(this);
        Closed?.Invoke();
    }

    // Back undoes whatever is in progress before it closes anything.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !UiPanelStack.IsTop(this) || !@event.IsActionPressed("ui_cancel"))
        {
            return;
        }

        if (!CancelInProgress())
        {
            Close();
        }

        GetViewport().SetInputAsHandled();
    }
}
