using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BuildMenu : Control
{
    [Signal] public delegate void ClosedEventHandler();

    [Export] public SpellGrid Grid { get; private set; }

    private Spell selectedSpell;
    private readonly Rune[] socketedRunes = new Rune[4];

    private Control socketScreen;
    private Label centerSpellLabel;
    private readonly Button[] socketButtons = new Button[4];

    private Control runePickerScreen;
    private SpellGrid runeGrid;
    private int socketBeingAssigned = -1;

    private ConfirmationDialog clearAllDialog;

    private IEnumerable<Rune> AllRunes => SaveManager.Instance.Data.KnownRunes.Select(entry => entry.Rune);

    public override void _Ready()
    {
        PopulateSpellGrid();
        BuildSocketScreen();
        BuildRunePickerScreen();
    }

    private void PopulateSpellGrid()
    {
        var spells = SaveManager.Instance.Data.KnownSpells;
        int pageSize = Grid.Columns * Grid.Rows;

        for (int i = 0; i < pageSize; i++)
        {
            if (i < spells.Count)
            {
                Spell spell = spells[i];
                Button button = CreateTileButton(spell.Name);
                button.Pressed += () => OnSpellSelected(spell);
                Grid.AddChild(button);
            }
            else
            {
                Grid.AddChild(CreateSpacer());
            }
        }
    }

    private void OnSpellSelected(Spell spell)
    {
        if (spell != selectedSpell)
        {
            for (int i = 0; i < socketedRunes.Length; i++)
            {
                socketedRunes[i] = null;
            }
        }

        selectedSpell = spell;
        ShowSocketScreen();
    }

    private void BuildSocketScreen()
    {
        socketScreen = new Control { Visible = false };
        socketScreen.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(socketScreen);

        centerSpellLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        centerSpellLabel.AddThemeFontSizeOverride("font_size", 28);
        centerSpellLabel.SetAnchorsPreset(LayoutPreset.Center);
        socketScreen.AddChild(centerSpellLabel);

        for (int i = 0; i < socketButtons.Length; i++)
        {
            int socketIndex = i;
            Button socketButton = new Button();
            CenterOnAnchor(socketButton, SocketAnchors[i], new Vector2(160f, 100f));
            socketButton.Pressed += () => ShowRunePicker(socketIndex);
            socketScreen.AddChild(socketButton);
            socketButtons[i] = socketButton;
        }

        Button clearAllButton = new Button { Text = "Clear All" };
        clearAllButton.SetAnchorsPreset(LayoutPreset.CenterBottom);
        clearAllButton.Pressed += () => clearAllDialog.PopupCentered();
        socketScreen.AddChild(clearAllButton);

        Button confirmButton = new Button { Text = "Confirm" };
        confirmButton.SetAnchorsPreset(LayoutPreset.CenterTop);
        confirmButton.Pressed += OnConfirmPressed;
        socketScreen.AddChild(confirmButton);

        clearAllDialog = new ConfirmationDialog { DialogText = "Remove all socketed runes?" };
        clearAllDialog.Confirmed += OnClearAllConfirmed;
        socketScreen.AddChild(clearAllDialog);
    }

    private void ShowSocketScreen()
    {
        Grid.Visible = false;
        runePickerScreen.Visible = false;
        socketScreen.Visible = true;

        RefreshSocketScreen();
        socketButtons[0].GrabFocus();
    }

    private void RefreshSocketScreen()
    {
        centerSpellLabel.Text = selectedSpell?.Name ?? "";

        for (int i = 0; i < socketButtons.Length; i++)
        {
            socketButtons[i].Text = socketedRunes[i]?.Name ?? "Empty";
        }
    }

    private void OnClearAllConfirmed()
    {
        for (int i = 0; i < socketedRunes.Length; i++)
        {
            socketedRunes[i] = null;
        }

        RefreshSocketScreen();
    }

    private void OnConfirmPressed()
    {
        string runeNames = string.Join(", ", socketedRunes.Where(r => r != null).Select(r => r.Name));
        GD.Print($"Built: {selectedSpell?.Name} [{runeNames}]");
        Close();
    }

    private void BuildRunePickerScreen()
    {
        runePickerScreen = new Control { Visible = false };
        runePickerScreen.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(runePickerScreen);

        runeGrid = new SpellGrid { Columns = Grid.Columns, Rows = Grid.Rows };
        runeGrid.AnchorLeft = 0f;
        runeGrid.AnchorRight = 1f;
        runeGrid.AnchorTop = 0f;
        runeGrid.AnchorBottom = 0.88f;
        runeGrid.AddThemeConstantOverride("h_separation", 8);
        runeGrid.AddThemeConstantOverride("v_separation", 8);
        runePickerScreen.AddChild(runeGrid);

        Button unsocketButton = new Button { Text = "Unsocket" };
        unsocketButton.SetAnchorsPreset(LayoutPreset.CenterBottom);
        unsocketButton.Pressed += () => OnRuneSelected(null);
        runePickerScreen.AddChild(unsocketButton);
    }

    private void ShowRunePicker(int socketIndex)
    {
        socketBeingAssigned = socketIndex;
        Rune currentSocketRune = socketedRunes[socketIndex];

        foreach (Node child in runeGrid.GetChildren())
        {
            child.QueueFree();
        }

        List<Rune> compatibleRunes = AllRunes
            .Where(rune => rune.RequiredTags.All(tag => selectedSpell.Tags.Contains(tag)))
            .OrderBy(rune => IsSocketedElsewhere(rune, socketIndex))
            .ToList();

        int pageSize = runeGrid.Columns * runeGrid.Rows;

        for (int i = 0; i < pageSize; i++)
        {
            if (i < compatibleRunes.Count)
            {
                Rune rune = compatibleRunes[i];
                bool isCurrentSocketRune = rune == currentSocketRune;
                bool disabled = IsSocketedElsewhere(rune, socketIndex);

                Button button = CreateTileButton(rune.Name);
                button.Disabled = disabled;

                if (isCurrentSocketRune)
                {
                    button.Modulate = new Color(1f, 0.85f, 0.3f);
                    button.Pressed += () => OnRuneSelected(null);
                }
                else if (!disabled)
                {
                    button.Pressed += () => OnRuneSelected(rune);
                }

                runeGrid.AddChild(button);
            }
            else
            {
                runeGrid.AddChild(CreateSpacer());
            }
        }

        Grid.Visible = false;
        socketScreen.Visible = false;
        runePickerScreen.Visible = true;
    }

    private bool IsSocketedElsewhere(Rune rune, int socketIndex)
    {
        for (int i = 0; i < socketedRunes.Length; i++)
        {
            if (i != socketIndex && socketedRunes[i] == rune)
            {
                return true;
            }
        }

        return false;
    }

    private void OnRuneSelected(Rune rune)
    {
        socketedRunes[socketBeingAssigned] = rune;
        ShowSocketScreen();
    }

    private static Button CreateTileButton(string text)
    {
        Button button = new Button
        {
            CustomMinimumSize = new Vector2(120f, 80f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        Label label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.SetAnchorsPreset(LayoutPreset.TopWide);
        button.AddChild(label);

        return button;
    }

    private static readonly Vector2[] SocketAnchors =
    {
        new Vector2(0.25f, 0.25f),
        new Vector2(0.75f, 0.25f),
        new Vector2(0.25f, 0.75f),
        new Vector2(0.75f, 0.75f)
    };

    private static void CenterOnAnchor(Control control, Vector2 anchorPoint, Vector2 size)
    {
        control.AnchorLeft = anchorPoint.X;
        control.AnchorRight = anchorPoint.X;
        control.AnchorTop = anchorPoint.Y;
        control.AnchorBottom = anchorPoint.Y;

        control.OffsetLeft = -size.X / 2f;
        control.OffsetRight = size.X / 2f;
        control.OffsetTop = -size.Y / 2f;
        control.OffsetBottom = size.Y / 2f;
    }

    private static Control CreateSpacer()
    {
        return new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
    }

    public void Open()
    {
        Visible = true;
        ShowSpellGrid();
    }

    private void ShowSpellGrid()
    {
        Grid.Visible = true;
        socketScreen.Visible = false;
        runePickerScreen.Visible = false;

        if (Grid.GetChildCount() > 0)
        {
            (Grid.GetChild(0) as Control)?.GrabFocus();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            GoBack();
            GetViewport().SetInputAsHandled();
        }
    }

    private void GoBack()
    {
        if (runePickerScreen.Visible)
        {
            ShowSocketScreen();
        }
        else if (socketScreen.Visible)
        {
            ShowSpellGrid();
        }
        else
        {
            Close();
        }
    }

    private void Close()
    {
        Visible = false;
        EmitSignal(SignalName.Closed);
    }
}
