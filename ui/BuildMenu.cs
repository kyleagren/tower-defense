using Godot;
using System.Collections.Generic;
using System.Linq;

// The Architect's construction menu, radial throughout: an element ring, that element's spells, then
// the Ritual Inscription, where runes come from a bucket ring, a rune ring and a tier wheel opened
// around the socket being filled, before the finished ritual is placed. The same flow authors
// tokens: the token wheel picks a slot, and saving writes the build there instead of placing it.
// Split by screen across BuildMenu.SpellScreens.cs, BuildMenu.Inscription.cs, BuildMenu.RuneScreens.cs
// and BuildMenu.Tokens.cs; this file holds what they share - which screen is up, input, backing out
// and closing.
public partial class BuildMenu : Control
{
    [Signal] public delegate void ClosedEventHandler();
    [Signal] public delegate void ConfirmedEventHandler(Spell spell, Godot.Collections.Array<Rune> runes);

    [Export] public RadialWheel Wheel { get; private set; }
    [Export] public Control CenterInfo { get; private set; }
    [Export] public Label TitleLabel { get; private set; }
    [Export] public Label CostLabel { get; private set; }
    [Export] public Label DescriptionLabel { get; private set; }
    [Export] public Control DescriptionPanel { get; private set; }
    [Export] public RitualInscription Inscription { get; private set; }
    [Export] public RadialWheel RuneWheel { get; private set; }
    [Export] public Control RuneInfoPanel { get; private set; }
    [Export] public Label RuneTitleLabel { get; private set; }
    [Export] public Label RuneCostLabel { get; private set; }
    [Export] public Label RuneDescriptionLabel { get; private set; }
    [Export] public TokenWheel Tokens { get; private set; }
    [Export] public Control TokenModeTag { get; private set; }
    [Export] public Label TokenModeLabel { get; private set; }

    private enum Screen
    {
        ElementRing,
        PinnedWheel,
        SpellRing,
        FavoritesRing,
        Inscription,
        BucketRing,
        RuneRing,
        TierWheel,
        TokenWheel
    }

    // A ring always has at least this many slots, so gaining an item fills a gap instead of
    // reshaping the ring. One with more becomes a spiral.
    private const int MinRingSlots = 8;

    private static readonly string[] _tokenSlotActions = { "token_slot_1", "token_slot_2", "token_slot_3" };

    private static readonly Color _unaffordableColor = new Color(1f, 0.35f, 0.35f);
    private static readonly Color _actionColor = new Color(0.25f, 0.3f, 0.4f);
    private static readonly Color _clearColor = new Color(0.65f, 0.15f, 0.15f);
    private static readonly Color _emptySocketColor = new Color(0.15f, 0.15f, 0.15f);

    private Screen _screen;
    private readonly RitualBuild _build = new();
    private EconomyManager _economy;

    private bool IsSpellScreen => _screen is Screen.ElementRing or Screen.PinnedWheel or Screen.SpellRing or Screen.FavoritesRing;
    private bool IsRuneScreen => _screen is Screen.BucketRing or Screen.RuneRing or Screen.TierWheel;

    public override void _Ready()
    {
        _economy = LevelContext.Current.Economy;
        Wheel.HighlightChanged += OnWheelHighlightChanged;
        Wheel.Committed += OnWheelCommitted;
        RuneWheel.HighlightChanged += OnWheelHighlightChanged;
        RuneWheel.Committed += OnWheelCommitted;
        Inscription.HighlightChanged += OnInscriptionHighlightChanged;
        Inscription.Tapped += OnInscriptionTapped;
        Inscription.Held += OnInscriptionHeld;
        Tokens.HighlightChanged += ShowTokenInfo;
        Tokens.Tapped += OnTokenWheelTapped;
        Tokens.Held += OnTokenWheelHeld;
    }

    public override void _Process(double delta)
    {
        bool focused = Visible && UiPanelStack.IsTop(this);
        Wheel.AcceptsInput = focused && IsSpellScreen;
        RuneWheel.AcceptsInput = focused && IsRuneScreen;
        Inscription.AcceptsInput = focused && _screen == Screen.Inscription;
        Tokens.AcceptsInput = focused && _screen == Screen.TokenWheel;
    }

    // With a token, opens straight into authoring it.
    public void Open(int token = -1)
    {
        Visible = true;
        UiPanelStack.Push(this, Close, capturesMouse: true);

        if (token >= 0)
        {
            BeginTokenAuthoring(token);
        }
        else
        {
            EndTokenAuthoring();
            ShowElementRing();
        }
    }

    private void SetScreen(Screen screen)
    {
        _screen = screen;

        Wheel.Visible = IsSpellScreen;
        CenterInfo.Visible = IsSpellScreen;
        Tokens.Visible = screen == Screen.TokenWheel;
        Inscription.Visible = !IsSpellScreen && screen != Screen.TokenWheel;
        RuneWheel.Visible = IsRuneScreen;

        if (!IsSpellScreen)
        {
            DescriptionPanel.Visible = false;
        }

        if (!IsRuneScreen)
        {
            RuneInfoPanel.Visible = false;
        }

        Inscription.FadeAllExcept(IsRuneScreen ? SocketSlot(_socketBeingAssigned) : -1);
    }

    // --- Shared wheel handling ---

    private void OnWheelCommitted(int slot)
    {
        switch (_screen)
        {
            case Screen.ElementRing when slot == FavoritesSlot:
                ShowPinnedWheel();
                break;

            case Screen.ElementRing:
                ShowSpellRing(GroupAt(slot));
                break;

            case Screen.PinnedWheel:
            case Screen.FavoritesRing when slot != NewBuildSlot:
                UseFavorite(_ringFavorites[slot]);
                break;

            case Screen.FavoritesRing:
                OnSpellChosen(_favoritesRingSpell);
                break;

            case Screen.SpellRing when FavoritesOf(_ringSpells[slot]).Any():
                ShowFavoritesRing(_ringSpells[slot]);
                break;

            case Screen.SpellRing:
                OnSpellChosen(_ringSpells[slot]);
                break;

            case Screen.BucketRing:
                ShowRuneRing(RuneTags.Buckets[slot]);
                break;

            case Screen.RuneRing when slot < _ringSubBuckets.Count:
                ShowRuneRing(_openBucket, _ringSubBuckets[slot]);
                break;

            case Screen.RuneRing:
                SocketRune(_ringRunes[slot - _ringSubBuckets.Count]);
                break;

            case Screen.TierWheel:
                OnTierWheelCommitted(slot);
                break;
        }
    }

    private void OnWheelHighlightChanged(int slot)
    {
        if (IsRuneScreen)
        {
            ShowRuneScreenInfo(slot);
        }
        else
        {
            ShowSpellScreenInfo(slot);
        }
    }

    private static void SetCostText(Label label, string text, bool unaffordable)
    {
        label.Text = text;

        if (unaffordable)
        {
            label.AddThemeColorOverride("font_color", _unaffordableColor);
        }
        else
        {
            label.RemoveThemeColorOverride("font_color");
        }
    }

    private static List<RadialWheelItem> PadToRing(IEnumerable<RadialWheelItem> items)
    {
        List<RadialWheelItem> padded = items.ToList();

        while (padded.Count < MinRingSlots)
        {
            padded.Add(null);
        }

        return padded;
    }

    // --- Input, backing out and closing ---

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !UiPanelStack.IsTop(this))
        {
            return;
        }

        // The key that opened the menu closes it from any screen; the others step back one screen.
        if (@event.IsActionPressed("open_build_menu"))
        {
            Close();
            GetViewport().SetInputAsHandled();
            return;
        }

        // R1 switches to the token wheel; 1/2/3, or the d-pad with R1 held, go straight to a token.
        if (@event.IsActionPressed("token_modifier") && _screen != Screen.TokenWheel)
        {
            ShowTokenWheel(savesBuild: false);
            GetViewport().SetInputAsHandled();
            return;
        }

        for (int token = 0; token < _tokenSlotActions.Length; token++)
        {
            if (@event.IsActionPressed(_tokenSlotActions[token]) && (@event is not InputEventJoypadButton || InputChords.TokenModifierHeld))
            {
                BeginTokenAuthoring(token);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        bool backPressed = @event.IsActionPressed("ui_cancel")
            || @event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true };

        if (backPressed)
        {
            GoBack();
            GetViewport().SetInputAsHandled();
        }
    }

    private void GoBack()
    {
        if (_screen == _closesOnBack)
        {
            Close();
            return;
        }

        switch (_screen)
        {
            case Screen.ElementRing when IsAuthoringToken:
                ShowTokenWheel(savesBuild: false, _authoringToken);
                break;

            case Screen.ElementRing:
                Close();
                break;

            case Screen.SpellRing:
                ShowElementRing(SlotOf(_openGroup));
                break;

            case Screen.PinnedWheel:
                ShowElementRing(FavoritesSlot);
                break;

            case Screen.FavoritesRing:
                ShowSpellRing(SpellWheelGroups.Of(_favoritesRingSpell), _favoritesRingSpell);
                break;

            // Back to the favorites ring on New build if the spell has one, otherwise the spell ring.
            case Screen.Inscription when FavoritesOf(_build.Spell).Any():
                ShowFavoritesRing(_build.Spell, NewBuildSlot);
                break;

            case Screen.Inscription:
                ShowSpellRing(SpellWheelGroups.Of(_build.Spell), _build.Spell);
                break;

            case Screen.BucketRing:
            case Screen.TierWheel:
                ShowInscription(SocketSlot(_socketBeingAssigned));
                break;

            case Screen.RuneRing when _openSubBucket.HasValue:
                ShowRuneRing(_openBucket, highlightedSubBucket: _openSubBucket);
                break;

            case Screen.RuneRing:
                ShowBucketRing(RuneTags.Buckets.ToList().IndexOf(_openBucket));
                break;

            case Screen.TokenWheel when _tokenWheelSavesBuild:
                ShowInscription(SaveTokenSlot);
                break;

            case Screen.TokenWheel:
                ShowElementRing();
                break;
        }
    }

    // Freed with its Architect when Combat starts, possibly still open - an entry left on the panel
    // stack would go on blocking combat input.
    public override void _ExitTree()
    {
        if (Visible)
        {
            UiPanelStack.Pop(this);
        }
    }

    private void Close()
    {
        EndTokenAuthoring();
        Visible = false;
        UiPanelStack.Pop(this);
        EmitSignal(SignalName.Closed);
    }
}
