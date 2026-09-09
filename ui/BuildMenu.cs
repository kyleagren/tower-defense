using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BuildMenu : Control
{
    [Signal] public delegate void ClosedEventHandler();
    [Signal] public delegate void ConfirmedEventHandler(Spell spell, Godot.Collections.Array<Rune> runes);

    [Export] public TileGrid Grid { get; private set; }
    [Export] public Control MenuRegion { get; private set; }
    [Export] public Label DescriptionTitleLabel { get; private set; }
    [Export] public Label DescriptionBodyLabel { get; private set; }

    private Spell _selectedSpell;
    private readonly Rune[] _socketedRunes = new Rune[GameRules.MaxRuneSlots];

    private Control _socketScreen;
    private Label _centerSpellLabel;
    private readonly Button[] _socketButtons = new Button[GameRules.MaxRuneSlots];

    private Control _runePickerScreen;
    private TileGrid _runeGrid;
    private int _socketBeingAssigned = -1;

    private ConfirmationDialog _clearAllDialog;

    private EconomyManager _economy;

    // Button styling lives in ui/build_menu_theme.tres, assigned to this menu's root Control and
    // inherited by every button created here. These name the theme's type variations — a button
    // left with no variation gets the default tile look.
    private static readonly StringName _confirmVariation = "ConfirmButton";
    private static readonly StringName _clearAllVariation = "ClearAllButton";
    private static readonly StringName _emptySocketVariation = "EmptySocketButton";
    private static readonly StringName _currentSocketVariation = "CurrentSocketButton";

    // One tile per rune type, not per owned rarity tier - collapses to the highest tier owned until
    // the tier picker (README Section 17) lets a socket deliberately hold a weaker one instead.
    private IEnumerable<Rune> AllRunes => SaveManager.Instance.Data.KnownRunes
        .Select(entry => entry.Rune)
        .GroupBy(rune => rune.GetType())
        .Select(group => group.OrderByDescending(rune => rune.Rarity).First());

    public override void _Ready()
    {
        _economy = EconomyManager.Instance;
        PopulateSpellGrid();
        BuildSocketScreen();
        BuildRunePickerScreen();
    }

    private void PopulateSpellGrid()
    {
        List<Spell> spells = SaveManager.Instance.Data.KnownSpells.OrderBy(spell => spell.Name).ToList();
        int pageSize = Grid.Columns * Grid.Rows;

        for (int i = 0; i < pageSize; i++)
        {
            if (i < spells.Count)
            {
                Spell spell = spells[i];
                Button button = CreateTileButton($"{spell.Name}\nCost: {spell.BaseManaCost}", spell.Name, spell.Description);
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
        if (spell != _selectedSpell)
        {
            for (int i = 0; i < _socketedRunes.Length; i++)
            {
                _socketedRunes[i] = null;
            }
        }

        _selectedSpell = spell;
        ShowSocketScreen();
    }

    private void BuildSocketScreen()
    {
        _socketScreen = new Control { Visible = false };
        _socketScreen.SetAnchorsPreset(LayoutPreset.FullRect);
        MenuRegion.AddChild(_socketScreen);

        _centerSpellLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _centerSpellLabel.AddThemeFontSizeOverride("font_size", 28);
        _centerSpellLabel.SetAnchorsPreset(LayoutPreset.Center);
        _socketScreen.AddChild(_centerSpellLabel);

        for (int i = 0; i < _socketButtons.Length; i++)
        {
            int socketIndex = i;
            Button socketButton = new Button();
            ControlLayout.CenterOnAnchor(socketButton, _socketAnchors[i], new Vector2(160f, 100f));
            socketButton.Pressed += () => ShowRunePicker(socketIndex);
            socketButton.MouseEntered += () => ShowSocketDescription(socketIndex);
            socketButton.FocusEntered += () => ShowSocketDescription(socketIndex);
            _socketScreen.AddChild(socketButton);
            _socketButtons[i] = socketButton;
        }

        Button clearAllButton = new Button { Text = "Clear All", ThemeTypeVariation = _clearAllVariation };
        ControlLayout.CenterOnAnchor(clearAllButton, new Vector2(1f / 3f, 0.95f), new Vector2(140f, 50f));
        clearAllButton.Pressed += () => _clearAllDialog.PopupCentered();
        _socketScreen.AddChild(clearAllButton);

        Button confirmButton = new Button { Text = "Confirm", ThemeTypeVariation = _confirmVariation };
        ControlLayout.CenterOnAnchor(confirmButton, new Vector2(2f / 3f, 0.95f), new Vector2(140f, 50f));
        confirmButton.Pressed += OnConfirmPressed;
        _socketScreen.AddChild(confirmButton);

        _clearAllDialog = new ConfirmationDialog { DialogText = "Remove all socketed runes?" };
        _clearAllDialog.Confirmed += OnClearAllConfirmed;
        _socketScreen.AddChild(_clearAllDialog);
    }

    private void ShowSocketScreen()
    {
        Grid.Visible = false;
        _runePickerScreen.Visible = false;
        _socketScreen.Visible = true;

        RefreshSocketScreen();
        ShowDescription(_selectedSpell?.Name, _selectedSpell?.Description);
        _socketButtons[0].GrabFocus();
    }

    private void RefreshSocketScreen()
    {
        int cost = ComputeManaCost(_socketedRunes);
        _centerSpellLabel.Text = $"{_selectedSpell?.Name}\nCost: {cost} / Mana: {_economy.Mana}";

        for (int i = 0; i < _socketButtons.Length; i++)
        {
            bool isEmpty = _socketedRunes[i] == null;
            _socketButtons[i].Text = _socketedRunes[i]?.Name ?? "Empty";

            // Empty string falls back to the theme's base Button type.
            _socketButtons[i].ThemeTypeVariation = isEmpty ? _emptySocketVariation : "";
        }
    }

    private int ComputeManaCost(IEnumerable<Rune> runes)
    {
        if (_selectedSpell == null)
        {
            return 0;
        }

        return SpellInstance.ResolvePreview(_selectedSpell, runes).ResolvedManaCost;
    }

    private void ShowSocketDescription(int socketIndex)
    {
        Rune rune = _socketedRunes[socketIndex];

        if (rune != null)
        {
            ShowDescription(rune.Name, rune.Description);
        }
        else
        {
            ShowDescription(_selectedSpell?.Name, _selectedSpell?.Description);
        }
    }

    private void OnClearAllConfirmed()
    {
        for (int i = 0; i < _socketedRunes.Length; i++)
        {
            _socketedRunes[i] = null;
        }

        RefreshSocketScreen();
    }

    private void OnConfirmPressed()
    {
        Godot.Collections.Array<Rune> confirmedRunes = new Godot.Collections.Array<Rune>(_socketedRunes.Where(r => r != null));
        EmitSignal(SignalName.Confirmed, _selectedSpell, confirmedRunes);
        Close();
    }

    private void BuildRunePickerScreen()
    {
        _runePickerScreen = new Control { Visible = false };
        _runePickerScreen.SetAnchorsPreset(LayoutPreset.FullRect);
        MenuRegion.AddChild(_runePickerScreen);

        _runeGrid = new TileGrid
        {
            Columns = Grid.Columns,
            Rows = Grid.Rows,
            AnchorLeft = 0f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 0.88f
        };
        _runeGrid.AddThemeConstantOverride("h_separation", 8);
        _runeGrid.AddThemeConstantOverride("v_separation", 8);
        _runePickerScreen.AddChild(_runeGrid);

        Button unsocketButton = new Button { Text = "Unsocket" };
        unsocketButton.SetAnchorsPreset(LayoutPreset.CenterBottom);
        unsocketButton.Pressed += () => OnRuneSelected(null);
        _runePickerScreen.AddChild(unsocketButton);
    }

    private void ShowRunePicker(int socketIndex)
    {
        _socketBeingAssigned = socketIndex;
        Rune currentSocketRune = _socketedRunes[socketIndex];

        foreach (Node child in _runeGrid.GetChildren())
        {
            child.QueueFree();
        }

        List<Rune> compatibleRunes = GetCompatibleRunes(socketIndex);
        int baseCost = ComputeManaCost(_socketedRunes);

        int pageSize = _runeGrid.Columns * _runeGrid.Rows;

        for (int i = 0; i < pageSize; i++)
        {
            if (i < compatibleRunes.Count)
            {
                Rune rune = compatibleRunes[i];
                bool isCurrentSocketRune = rune == currentSocketRune;
                bool disabled = IsSocketedElsewhere(rune, socketIndex);

                int costDelta = ComputeManaCost(WithSocket(socketIndex, rune)) - baseCost;
                Button button = CreateTileButton($"{rune.Name}\n+{costDelta}", rune.Name, rune.Description);
                ItemSlotBackground.Set(button, RarityColors.Get(rune.Rarity));
                button.Disabled = disabled;

                if (isCurrentSocketRune)
                {
                    button.ThemeTypeVariation = _currentSocketVariation;
                    button.Pressed += () => OnRuneSelected(null);
                }
                else if (!disabled)
                {
                    button.Pressed += () => OnRuneSelected(rune);
                }

                _runeGrid.AddChild(button);
            }
            else
            {
                _runeGrid.AddChild(CreateSpacer());
            }
        }

        Grid.Visible = false;
        _socketScreen.Visible = false;
        _runePickerScreen.Visible = true;

        ShowDescription(currentSocketRune?.Name, currentSocketRune?.Description);
    }

    private List<Rune> GetCompatibleRunes(int socketIndex)
    {
        return AllRunes
            .Where(rune => rune.IsCompatibleWith(_selectedSpell.Tags))
            .OrderBy(rune => IsSocketedElsewhere(rune, socketIndex))
            .ThenBy(rune => rune.Name)
            .ToList();
    }

    private Rune[] WithSocket(int socketIndex, Rune rune)
    {
        Rune[] result = (Rune[])_socketedRunes.Clone();
        result[socketIndex] = rune;
        return result;
    }

    private bool IsSocketedElsewhere(Rune rune, int socketIndex)
    {
        for (int i = 0; i < _socketedRunes.Length; i++)
        {
            if (i != socketIndex && _socketedRunes[i] == rune)
            {
                return true;
            }
        }

        return false;
    }

    private void OnRuneSelected(Rune rune)
    {
        _socketedRunes[_socketBeingAssigned] = rune;
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

        button.MouseEntered += () => ShowDescription(descriptionTitle, descriptionBody);
        button.FocusEntered += () => ShowDescription(descriptionTitle, descriptionBody);

        return button;
    }

    private void ShowDescription(string title, string body)
    {
        DescriptionTitleLabel.Text = title ?? "";
        DescriptionBodyLabel.Text = body ?? "";
    }

    private static readonly Vector2[] _socketAnchors =
    {
        new Vector2(0.25f, 0.25f),
        new Vector2(0.75f, 0.25f),
        new Vector2(0.25f, 0.75f),
        new Vector2(0.75f, 0.75f)
    };

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
        UiPanelStack.Push(this, Close);
        ShowSpellGrid();
    }

    private void ShowSpellGrid()
    {
        Grid.Visible = true;
        _socketScreen.Visible = false;
        _runePickerScreen.Visible = false;

        if (Grid.GetChildCount() > 0)
        {
            (Grid.GetChild(0) as Control)?.GrabFocus();
        }

        ShowDescription(null, null);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !UiPanelStack.IsTop(this))
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
        if (_runePickerScreen.Visible)
        {
            ShowSocketScreen();
        }
        else if (_socketScreen.Visible)
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
        UiPanelStack.Pop(this);
        EmitSignal(SignalName.Closed);
    }
}
