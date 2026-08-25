using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BuildMenu : Control
{
    [Signal] public delegate void ClosedEventHandler();
    [Signal] public delegate void ConfirmedEventHandler(Spell spell, Godot.Collections.Array<Rune> runes);

    [Export] public SpellGrid Grid { get; private set; }
    [Export] public Control MenuRegion { get; private set; }
    [Export] public Label DescriptionTitleLabel { get; private set; }
    [Export] public Label DescriptionBodyLabel { get; private set; }

    private Spell selectedSpell;
    private readonly Rune[] socketedRunes = new Rune[GameRules.MaxRuneSlots];

    private Control socketScreen;
    private Label centerSpellLabel;
    private readonly Button[] socketButtons = new Button[GameRules.MaxRuneSlots];

    private Control runePickerScreen;
    private SpellGrid runeGrid;
    private int socketBeingAssigned = -1;

    private ConfirmationDialog clearAllDialog;

    private EconomyManager economy;

    private IEnumerable<Rune> AllRunes => SaveManager.Instance.Data.KnownRunes.Select(entry => entry.Rune);

    public override void _Ready()
    {
        economy = GetTree().CurrentScene.GetNode<EconomyManager>("EconomyManager");
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
                Button button = CreateTileButton($"{spell.Name}\nCost: {spell.BudgetCost}", spell.Name, spell.Description);
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
        MenuRegion.AddChild(socketScreen);

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
            socketButton.MouseEntered += () => ShowSocketDescription(socketIndex);
            socketButton.FocusEntered += () => ShowSocketDescription(socketIndex);
            socketScreen.AddChild(socketButton);
            socketButtons[i] = socketButton;
        }

        Button clearAllButton = new Button { Text = "Clear All" };
        CenterOnAnchor(clearAllButton, new Vector2(0.5f, 0.95f), new Vector2(140f, 50f));
        clearAllButton.AddThemeStyleboxOverride("normal", ClearAllButtonStyle);
        clearAllButton.AddThemeStyleboxOverride("hover", ClearAllButtonHoverStyle);
        clearAllButton.AddThemeStyleboxOverride("pressed", ClearAllButtonPressedStyle);
        clearAllButton.Pressed += () => clearAllDialog.PopupCentered();
        socketScreen.AddChild(clearAllButton);

        Button confirmButton = new Button { Text = "Confirm" };
        CenterOnAnchor(confirmButton, new Vector2(0.5f, 0.05f), new Vector2(140f, 50f));
        confirmButton.AddThemeStyleboxOverride("normal", ConfirmButtonStyle);
        confirmButton.AddThemeStyleboxOverride("hover", ConfirmButtonHoverStyle);
        confirmButton.AddThemeStyleboxOverride("pressed", ConfirmButtonPressedStyle);
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
        ShowDescription(selectedSpell?.Name, selectedSpell?.Description);
        socketButtons[0].GrabFocus();
    }

    private void RefreshSocketScreen()
    {
        int runeCount = socketedRunes.Count(r => r != null);
        int cost = selectedSpell != null ? economy.GetTowerCost(selectedSpell, runeCount) : 0;
        centerSpellLabel.Text = $"{selectedSpell?.Name}\nCost: {cost} / Budget: {economy.Budget}";

        for (int i = 0; i < socketButtons.Length; i++)
        {
            bool isEmpty = socketedRunes[i] == null;
            socketButtons[i].Text = socketedRunes[i]?.Name ?? "Empty";

            socketButtons[i].AddThemeStyleboxOverride("normal", isEmpty ? EmptySocketStyle : NormalTileStyle);
            socketButtons[i].AddThemeStyleboxOverride("hover", isEmpty ? EmptySocketHoverStyle : HoverTileStyle);
            socketButtons[i].AddThemeStyleboxOverride("pressed", isEmpty ? EmptySocketPressedStyle : PressedTileStyle);
        }
    }

    private void ShowSocketDescription(int socketIndex)
    {
        Rune rune = socketedRunes[socketIndex];

        if (rune != null)
        {
            ShowDescription(rune.Name, rune.Description);
        }
        else
        {
            ShowDescription(selectedSpell?.Name, selectedSpell?.Description);
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
        Godot.Collections.Array<Rune> confirmedRunes = new Godot.Collections.Array<Rune>(socketedRunes.Where(r => r != null));
        EmitSignal(SignalName.Confirmed, selectedSpell, confirmedRunes);
        Close();
    }

    private void BuildRunePickerScreen()
    {
        runePickerScreen = new Control { Visible = false };
        runePickerScreen.SetAnchorsPreset(LayoutPreset.FullRect);
        MenuRegion.AddChild(runePickerScreen);

        runeGrid = new SpellGrid
        {
            Columns = Grid.Columns,
            Rows = Grid.Rows,
            AnchorLeft = 0f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 0.88f
        };
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

                Button button = CreateTileButton($"{rune.Name}\n+{economy.BudgetPerRune}", rune.Name, rune.Description);
                button.Disabled = disabled;

                if (isCurrentSocketRune)
                {
                    button.AddThemeStyleboxOverride("normal", CurrentSocketTileStyle);
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

        ShowDescription(currentSocketRune?.Name, currentSocketRune?.Description);
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

    private Button CreateTileButton(string buttonText, string descriptionTitle, string descriptionBody)
    {
        Button button = new Button
        {
            CustomMinimumSize = new Vector2(120f, 80f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        Label label = new Label
        {
            Text = buttonText,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.SetAnchorsPreset(LayoutPreset.TopWide);
        button.AddChild(label);

        button.AddThemeStyleboxOverride("normal", NormalTileStyle);
        button.AddThemeStyleboxOverride("hover", HoverTileStyle);
        button.AddThemeStyleboxOverride("pressed", PressedTileStyle);
        button.AddThemeStyleboxOverride("disabled", DisabledTileStyle);

        button.MouseEntered += () => ShowDescription(descriptionTitle, descriptionBody);
        button.FocusEntered += () => ShowDescription(descriptionTitle, descriptionBody);

        return button;
    }

    private static readonly StyleBoxFlat NormalTileStyle = CreateTileStyle(new Color(0.25f, 0.45f, 0.65f));
    private static readonly StyleBoxFlat HoverTileStyle = CreateTileStyle(new Color(0.32f, 0.55f, 0.78f));
    private static readonly StyleBoxFlat PressedTileStyle = CreateTileStyle(new Color(0.18f, 0.32f, 0.48f));
    private static readonly StyleBoxFlat DisabledTileStyle = CreateTileStyle(new Color(0.18f, 0.18f, 0.18f));
    private static readonly StyleBoxFlat CurrentSocketTileStyle = CreateTileStyle(new Color(0.75f, 0.6f, 0.15f));
    private static readonly StyleBoxFlat ConfirmButtonStyle = CreateTileStyle(new Color(0.2f, 0.55f, 0.25f));
    private static readonly StyleBoxFlat ConfirmButtonHoverStyle = CreateTileStyle(new Color(0.28f, 0.68f, 0.32f));
    private static readonly StyleBoxFlat ConfirmButtonPressedStyle = CreateTileStyle(new Color(0.15f, 0.4f, 0.18f));
    private static readonly StyleBoxFlat ClearAllButtonStyle = CreateTileStyle(new Color(0.6f, 0.2f, 0.2f));
    private static readonly StyleBoxFlat ClearAllButtonHoverStyle = CreateTileStyle(new Color(0.72f, 0.28f, 0.28f));
    private static readonly StyleBoxFlat ClearAllButtonPressedStyle = CreateTileStyle(new Color(0.45f, 0.15f, 0.15f));
    private static readonly StyleBoxFlat EmptySocketStyle = CreateBorderedTileStyle(new Color(0.15f, 0.15f, 0.15f), new Color(0.25f, 0.45f, 0.65f));
    private static readonly StyleBoxFlat EmptySocketHoverStyle = CreateBorderedTileStyle(new Color(0.2f, 0.2f, 0.2f), new Color(0.32f, 0.55f, 0.78f));
    private static readonly StyleBoxFlat EmptySocketPressedStyle = CreateBorderedTileStyle(new Color(0.1f, 0.1f, 0.1f), new Color(0.18f, 0.32f, 0.48f));

    private static StyleBoxFlat CreateTileStyle(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
    }

    private static StyleBoxFlat CreateBorderedTileStyle(Color bgColor, Color borderColor)
    {
        StyleBoxFlat style = CreateTileStyle(bgColor);
        style.BorderColor = borderColor;
        style.BorderWidthLeft = 3;
        style.BorderWidthRight = 3;
        style.BorderWidthTop = 3;
        style.BorderWidthBottom = 3;
        return style;
    }

    private void ShowDescription(string title, string body)
    {
        DescriptionTitleLabel.Text = title ?? "";
        DescriptionBodyLabel.Text = body ?? "";
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

        ShowDescription(null, null);
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
