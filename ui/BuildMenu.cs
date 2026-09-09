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
    private readonly Label[] _socketLabels = new Label[GameRules.MaxRuneSlots];

    private Control _runePickerScreen;
    private TileGrid _runeGrid;
    private int _socketBeingAssigned = -1;

    private Control _tierPopup;
    private int _tierPopupSocketIndex = -1;
    private const float TierRowHeight = 100f;
    private const float TierRowPadding = 2f;
    private const float TierRowPitch = TierRowHeight + TierRowPadding * 2f;

    private ConfirmationDialog _clearAllDialog;

    private EconomyManager _economy;

    // Button styling lives in ui/build_menu_theme.tres, assigned to this menu's root Control and
    // inherited by every button created here. These name the theme's type variations — a button
    // left with no variation gets the default tile look.
    private static readonly StringName _confirmVariation = "ConfirmButton";
    private static readonly StringName _clearAllVariation = "ClearAllButton";
    private static readonly StringName _emptySocketVariation = "EmptySocketButton";

    // Same golden color the old CurrentSocketButton theme variation used - kept as a border now
    // instead of a full fill, so it can sit on top of a rune's rarity color instead of hiding it.
    private static readonly Color _currentTierBorderColor = new Color(0.75f, 0.6f, 0.15f);

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
        BuildTierPopup();
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
        _centerSpellLabel.AnchorLeft = 0f;
        _centerSpellLabel.AnchorRight = 1f;
        _centerSpellLabel.AnchorTop = 0.5f;
        _centerSpellLabel.AnchorBottom = 0.5f;
        _socketScreen.AddChild(_centerSpellLabel);

        for (int i = 0; i < _socketButtons.Length; i++)
        {
            int socketIndex = i;
            Button socketButton = new Button();
            ControlLayout.CenterOnAnchor(socketButton, _socketAnchors[i], new Vector2(TierRowHeight, TierRowHeight));
            socketButton.Pressed += () => OnSocketButtonPressed(socketIndex);
            socketButton.MouseEntered += () => ShowSocketDescription(socketIndex);
            socketButton.FocusEntered += () => ShowSocketDescription(socketIndex);
            _socketScreen.AddChild(socketButton);
            _socketButtons[i] = socketButton;
            _socketLabels[i] = AddCenteredLabel(socketButton, "");
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
            _socketLabels[i].Text = _socketedRunes[i]?.Name ?? "Empty";

            // Empty string falls back to the theme's base Button type.
            _socketButtons[i].ThemeTypeVariation = isEmpty ? _emptySocketVariation : "";

            if (isEmpty)
            {
                ItemSlotBackground.Clear(_socketButtons[i]);
            }
            else
            {
                SetCircularBackground(_socketButtons[i], RarityColors.Get(_socketedRunes[i].Rarity));
            }
        }
    }

    // Empty socket: straight to the picker, same as always. Filled socket: the tier popup instead,
    // since there's now a choice between "browse a different rune" and "pick a different owned
    // tier of this same one."
    private void OnSocketButtonPressed(int socketIndex)
    {
        if (_socketedRunes[socketIndex] != null)
        {
            ShowTierPopup(socketIndex);
        }
        else
        {
            ShowRunePicker(socketIndex);
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

    private void BuildTierPopup()
    {
        _tierPopup = new Control { Visible = false };
        _tierPopup.SetAnchorsPreset(LayoutPreset.FullRect);
        _socketScreen.AddChild(_tierPopup);
    }

    // Each row is anchored at the same fractional point the socket itself uses, offset by whole
    // row heights - rowSlot 0 is exactly the socket's own rect ("home base"), negative slots stack
    // upward, positive slots stack downward.
    private void PositionPopupRow(Control row, int socketIndex, int rowSlot)
    {
        Vector2 anchor = _socketAnchors[socketIndex];
        float verticalOffset = rowSlot * TierRowPitch;

        row.AnchorLeft = anchor.X;
        row.AnchorRight = anchor.X;
        row.AnchorTop = anchor.Y;
        row.AnchorBottom = anchor.Y;
        row.OffsetLeft = -TierRowHeight / 2f;
        row.OffsetRight = TierRowHeight / 2f;
        row.OffsetTop = -TierRowHeight / 2f + verticalOffset;
        row.OffsetBottom = TierRowHeight / 2f + verticalOffset;
    }

    // Fixed, immediately left of current - never recalculated, since current always sits at the
    // socket's own position regardless of which tier that is.
    private void PositionXButton(Control x, int socketIndex)
    {
        Vector2 anchor = _socketAnchors[socketIndex];

        x.AnchorLeft = anchor.X;
        x.AnchorRight = anchor.X;
        x.AnchorTop = anchor.Y;
        x.AnchorBottom = anchor.Y;
        x.OffsetLeft = -TierRowHeight * 1.5f;
        x.OffsetRight = -TierRowHeight * 0.5f;
        x.OffsetTop = -TierRowHeight / 2f;
        x.OffsetBottom = TierRowHeight / 2f;
    }

    // Circular styling for rune sockets/tier rows specifically - kept local to BuildMenu rather
    // than added to the shared ItemSlotBackground, since Vendor/Crafting still want plain
    // rectangular slots. A large fixed corner radius reads as fully round at any button size
    // without needing to compute an exact half-size radius.
    private static StyleBoxFlat BuildCircularStyle(Color fillColor, Color? borderColor = null)
    {
        StyleBoxFlat style = new StyleBoxFlat
        {
            BgColor = fillColor,
            CornerRadiusTopLeft = 999,
            CornerRadiusTopRight = 999,
            CornerRadiusBottomLeft = 999,
            CornerRadiusBottomRight = 999
        };

        if (borderColor.HasValue)
        {
            style.BorderColor = borderColor.Value;
            style.SetBorderWidthAll(3);
        }

        return style;
    }

    private static void SetCircularBackground(Button button, Color color)
    {
        StyleBoxFlat style = BuildCircularStyle(color);
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
    }

    // Transparent so the single shared background panel behind the whole tier popup shows through
    // instead of each row drawing its own opaque square (which was clipping the focus ring on
    // whichever edge touched a later-drawn neighbor).
    private static void SetTransparentBackground(Button button)
    {
        StyleBoxFlat style = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
    }

    // Just a centered label - the tier popup's shared black background panel already provides the
    // "black bar" behind it; no separate backdrop element needed.
    private static Label AddCenteredLabel(Button button, string text)
    {
        Label label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        button.AddChild(label);

        return label;
    }

    // Current tier boxed and focused by default at rowSlot 0, every owned tier up to the player's
    // actual max for this rune type shown around it (not just tiers below current - a deliberate
    // downgrade shouldn't hide the tiers you dropped down from). Fixed order: Legendary always
    // above Common, regardless of which socket this is - _socketAnchors gives enough headroom in
    // both directions for that not to need per-socket direction-flipping.
    private void ShowTierPopup(int socketIndex)
    {
        _tierPopupSocketIndex = socketIndex;
        Rune currentRune = _socketedRunes[socketIndex];

        foreach (Node child in _tierPopup.GetChildren())
        {
            child.QueueFree();
        }

        Rune maxOwnedTier = SaveManager.Instance.Data.KnownRunes
            .Select(entry => entry.Rune)
            .Where(rune => rune.GetType() == currentRune.GetType())
            .OrderByDescending(rune => rune.Rarity)
            .First();

        List<Rune> tiers = RuneCatalog.GetRunesOfType(currentRune)
            .Where(r => r.Rarity <= maxOwnedTier.Rarity)
            .OrderByDescending(r => r.Rarity)
            .ToList();

        int currentIndex = tiers.IndexOf(currentRune);
        int RowSlotFor(int listIndex) => listIndex - currentIndex;

        // One shared background behind the tier column only - later-added rows draw on top of
        // earlier ones with zero gap between them, which was clipping the focus ring on whichever
        // edge touched a later-drawn neighbor. Sized to the actual topmost-to-bottommost tier
        // span, which varies with how many tiers are owned. X gets its own separate black square
        // below rather than sharing this column, since it isn't part of the vertical stack.
        int topSlot = RowSlotFor(0);
        int bottomSlot = RowSlotFor(tiers.Count - 1);

        Vector2 anchor = _socketAnchors[socketIndex];
        Panel background = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        background.AnchorLeft = anchor.X;
        background.AnchorRight = anchor.X;
        background.AnchorTop = anchor.Y;
        background.AnchorBottom = anchor.Y;
        background.OffsetLeft = -TierRowHeight / 2f;
        background.OffsetRight = TierRowHeight / 2f;
        background.OffsetTop = -TierRowHeight / 2f + topSlot * TierRowPitch;
        background.OffsetBottom = TierRowHeight / 2f + bottomSlot * TierRowPitch;
        background.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = Colors.Black });
        _tierPopup.AddChild(background);

        Button removeButton = new Button();
        removeButton.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = Colors.Black });
        removeButton.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = Colors.Black });

        Label xLabel = new Label
        {
            Text = "X",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        xLabel.AddThemeColorOverride("font_color", Colors.Red);
        xLabel.AddThemeFontSizeOverride("font_size", 48);
        xLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        removeButton.AddChild(xLabel);

        removeButton.Pressed += () =>
        {
            _socketedRunes[socketIndex] = null;
            HideTierPopup();
            RefreshSocketScreen();
        };
        PositionXButton(removeButton, socketIndex);
        _tierPopup.AddChild(removeButton);

        Button currentTierButton = null;

        for (int i = 0; i < tiers.Count; i++)
        {
            Rune tier = tiers[i];

            Button tierButton = new Button();
            AddCenteredLabel(tierButton, tier.Name);
            PositionPopupRow(tierButton, socketIndex, RowSlotFor(i));

            SetTransparentBackground(tierButton);

            // Same size as the button itself - the outer square stays square-cornered, and a
            // circle at full size still can't cover a square's corners, so the black frame shows
            // through there without needing an artificial margin. Moved behind the label
            // AddCenteredLabel already added, so the text stays readable on top of it rather than
            // the circle covering it.
            Panel innerCircle = new Panel { MouseFilter = MouseFilterEnum.Ignore };
            innerCircle.SetAnchorsPreset(LayoutPreset.FullRect);
            innerCircle.AddThemeStyleboxOverride("panel", BuildCircularStyle(RarityColors.Get(tier.Rarity)));
            tierButton.AddChild(innerCircle);
            tierButton.MoveChild(innerCircle, 0);

            if (tier == currentRune)
            {
                Label arrowLabel = new Label { Text = "▸", MouseFilter = MouseFilterEnum.Ignore };
                arrowLabel.SetAnchorsPreset(LayoutPreset.CenterRight);
                arrowLabel.OffsetLeft = -20f;
                tierButton.AddChild(arrowLabel);

                tierButton.Pressed += () =>
                {
                    HideTierPopup();
                    ShowRunePicker(socketIndex);
                };
                currentTierButton = tierButton;
            }
            else
            {
                tierButton.Pressed += () =>
                {
                    _socketedRunes[socketIndex] = tier;
                    HideTierPopup();
                    RefreshSocketScreen();
                };
            }

            _tierPopup.AddChild(tierButton);
        }

        _tierPopup.Visible = true;
        AnimateSocketExpand(socketIndex, true);
        currentTierButton?.GrabFocus();
    }

    private void HideTierPopup()
    {
        if (_tierPopupSocketIndex >= 0)
        {
            AnimateSocketExpand(_tierPopupSocketIndex, false);
        }

        _tierPopup.Visible = false;
        _tierPopupSocketIndex = -1;
    }

    // Scales the socket button up slightly around its own center (not the default top-left pivot)
    // while its popup is open, as a lightweight "this is the one being edited" cue.
    private void AnimateSocketExpand(int socketIndex, bool expand)
    {
        Button socketButton = _socketButtons[socketIndex];
        socketButton.PivotOffset = socketButton.Size / 2f;

        Tween tween = CreateTween();
        tween.TweenProperty(socketButton, "scale", expand ? new Vector2(1.06f, 1.06f) : Vector2.One, 0.12f);
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
                bool disabled = IsSocketedElsewhere(rune, socketIndex) || !RuneCatalog.IsAvailable(rune);

                int costDelta = ComputeManaCost(WithSocket(socketIndex, rune)) - baseCost;
                Button button = CreateTileButton($"{rune.Name}\n+{costDelta}", rune.Name, rune.Description);
                button.Disabled = disabled;

                if (disabled)
                {
                    ItemSlotBackground.SetUnavailable(button);
                }
                else if (isCurrentSocketRune)
                {
                    ItemSlotBackground.SetBorder(button, RarityColors.Get(rune.Rarity), _currentTierBorderColor);
                }
                else
                {
                    ItemSlotBackground.Set(button, RarityColors.Get(rune.Rarity));
                }

                if (!disabled)
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
            .OrderBy(rune => IsSocketedElsewhere(rune, socketIndex) || !RuneCatalog.IsAvailable(rune))
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
            if (i != socketIndex && _socketedRunes[i] != null && _socketedRunes[i].GetType() == rune.GetType())
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

    // X no longer sits in the vertical stack, so worst case is 4 rows (not 5) in a single
    // direction from current - at TierRowHeight px that's ~450px needed above and below any
    // socket alike (fixed ordering still means both directions matter for every socket). X
    // spread to match, not the same fraction as Y - the 2560x1440 viewport is wider than tall,
    // so equal fractions would give a visibly wide rectangle; these keep the actual on-screen
    // distance from center roughly equal in both directions (~216px X, ~216px Y).
    private static readonly Vector2[] _socketAnchors =
    {
        new Vector2(0.42f, 0.35f),
        new Vector2(0.58f, 0.35f),
        new Vector2(0.42f, 0.65f),
        new Vector2(0.58f, 0.65f)
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
            if (_tierPopup.Visible)
            {
                HideTierPopup();
            }
            else
            {
                GoBack();
            }

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
