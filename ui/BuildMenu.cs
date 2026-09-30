using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// The Architect's construction menu, radial throughout: an element ring, that element's spells, then
// the Ritual Inscription, where runes come from a bucket ring, a rune ring and a tier wheel opened
// around the socket being filled, before the finished ritual is placed.
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

    private enum Screen
    {
        ElementRing,
        PinnedWheel,
        SpellRing,
        FavoritesRing,
        Inscription,
        BucketRing,
        RuneRing,
        TierWheel
    }

    // A ring always has at least this many slots, so gaining an item fills a gap instead of
    // reshaping the ring. One with more than this grows until the spiral exists.
    private const int MinRingSlots = 8;

    // The element ring's top wedge; the element groups follow it clockwise.
    private const int FavoritesSlot = 0;

    // A spell's favorites ring: New build at the top, then its favorites in saved order.
    private const int NewBuildSlot = 0;

    // The inscription's actions, on the cardinals clockwise from the top. Sockets fill the diagonals
    // between.
    private const int InscriptionSlots = 8;
    private const int SaveFavoriteSlot = 0;
    private const int ClearAllSlot = 2;
    private const int PlaceSlot = 4;
    private const int SaveTokenSlot = 6;

    // Held to confirm, like repair and destroy, since clearing a near-finished build can't be undone.
    private const float ClearAllHoldSeconds = 1f;

    // A filled socket: tap swaps its rune, holding past the same threshold ability slots use opens
    // the tier wheel.
    private const float TierHoldSeconds = GameRules.AbilityHoldThreshold;

    // The tier wheel: Swap at the top, the tiers rising clockwise from the top-right, Clear on the left.
    private const int TierWheelSlots = 8;
    private const int SwapSlot = 0;
    private const int FirstTierSlot = 1;
    private const int ClearSocketSlot = 6;

    // The rune wheel wraps the socket being filled: its hole is the socket, plus a gap that keeps the
    // socket's highlight border visible.
    private const float RuneRingGap = 4f;
    private const float RuneRingThickness = 150f;

    // The rune info panel sits on the ring's side of the inscription - above for the top sockets,
    // below for the bottom ones - and grows away from the ring when a description runs long.
    private const float RuneInfoNearEdge = 545f;
    private const float RuneInfoFarEdge = 685f;

    private static readonly SpellWheelGroup[] _groups = Enum.GetValues<SpellWheelGroup>();
    private static readonly Rarity[] _runeTiers = { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary };

    private static readonly Color _favoritesColor = new Color(1f, 0.8f, 0.3f);
    private static readonly Color _unaffordableColor = new Color(1f, 0.35f, 0.35f);
    private static readonly Color _actionColor = new Color(0.25f, 0.3f, 0.4f);
    private static readonly Color _placeColor = new Color(0.2f, 0.45f, 0.25f);
    private static readonly Color _clearColor = new Color(0.65f, 0.15f, 0.15f);
    private static readonly Color _emptySocketColor = new Color(0.15f, 0.15f, 0.15f);
    private static readonly Color _bucketColor = new Color(0.3f, 0.3f, 0.35f);

    private Screen _screen;
    private SpellWheelGroup _openGroup;
    private readonly List<Spell> _ringSpells = new();

    // What each slot of the favorites ring or pinned wheel holds; null for New build or an empty pin.
    private readonly List<FavoriteLoadout> _ringFavorites = new();
    private Spell _favoritesRingSpell;

    private Spell _selectedSpell;
    private readonly Rune[] _socketedRunes = new Rune[GameRules.MaxRuneSlots];
    private int _socketBeingAssigned = -1;

    private Tag _openBucket;
    private readonly List<Rune> _ringRunes = new();
    private readonly Rune[] _tierRunes = new Rune[5];

    private EconomyManager _economy;

    private static IEnumerable<Spell> KnownSpells => SaveManager.Instance.Data.KnownSpells;

    private static IEnumerable<Rune> KnownRunes => SaveManager.Instance.Data.KnownRunes.Select(entry => entry.Rune);

    // One per rune type, at the highest tier owned - the tier wheel is what picks a lower one.
    private static IEnumerable<Rune> OwnedRunes => KnownRunes
        .GroupBy(rune => rune.GetType())
        .Select(group => group.OrderByDescending(rune => rune.Rarity).First());

    private bool IsSpellScreen => _screen is Screen.ElementRing or Screen.PinnedWheel or Screen.SpellRing or Screen.FavoritesRing;
    private bool IsRuneScreen => _screen is Screen.BucketRing or Screen.RuneRing or Screen.TierWheel;

    public override void _Ready()
    {
        _economy = EconomyManager.Instance;
        Wheel.HighlightChanged += OnWheelHighlightChanged;
        Wheel.Committed += OnWheelCommitted;
        RuneWheel.HighlightChanged += OnWheelHighlightChanged;
        RuneWheel.Committed += OnWheelCommitted;
        Inscription.HighlightChanged += OnInscriptionHighlightChanged;
        Inscription.Tapped += OnInscriptionTapped;
        Inscription.Held += OnInscriptionHeld;
    }

    public override void _Process(double delta)
    {
        bool focused = Visible && UiPanelStack.IsTop(this);
        Wheel.AcceptsInput = focused && IsSpellScreen;
        RuneWheel.AcceptsInput = focused && IsRuneScreen;
        Inscription.AcceptsInput = focused && _screen == Screen.Inscription;
    }

    public void Open()
    {
        Visible = true;
        UiPanelStack.Push(this, Close, capturesMouse: true);
        ShowElementRing();
    }

    private void SetScreen(Screen screen)
    {
        _screen = screen;

        Wheel.Visible = IsSpellScreen;
        CenterInfo.Visible = IsSpellScreen;
        Inscription.Visible = !IsSpellScreen;
        RuneWheel.Visible = IsRuneScreen;

        if (!IsSpellScreen)
        {
            DescriptionPanel.Visible = false;
        }

        if (!IsRuneScreen)
        {
            RuneInfoPanel.Visible = false;
        }

        Inscription.SetFocusSocket(IsRuneScreen ? SocketSlot(_socketBeingAssigned) : -1);
    }

    // --- Element and spell rings ---

    private void ShowElementRing(int highlighted = -1)
    {
        SetScreen(Screen.ElementRing);

        List<RadialWheelItem> items = new List<RadialWheelItem>
        {
            new RadialWheelItem { Label = "Favorites", Color = _favoritesColor, Enabled = AnythingPinned }
        };

        items.AddRange(_groups.Select(group => new RadialWheelItem
        {
            Label = SpellWheelGroups.NameOf(group),
            Color = SpellWheelGroups.ColorOf(group),
            Enabled = KnownSpells.Any(spell => SpellWheelGroups.Of(spell) == group)
        }));

        Wheel.SetItems(items, highlighted);
        ShowSpellScreenInfo(highlighted);
    }

    private static SpellWheelGroup GroupAt(int slot) => _groups[slot - FavoritesSlot - 1];

    private static int SlotOf(SpellWheelGroup group) => Array.IndexOf(_groups, group) + FavoritesSlot + 1;

    private void ShowSpellRing(SpellWheelGroup group, Spell highlight = null)
    {
        SetScreen(Screen.SpellRing);
        _openGroup = group;
        _ringSpells.Clear();
        _ringSpells.AddRange(KnownSpells
            .Where(spell => SpellWheelGroups.Of(spell) == group)
            .OrderBy(spell => spell.WheelOrder)
            .ThenBy(spell => spell.Name));

        int highlighted = _ringSpells.IndexOf(highlight);

        Wheel.SetItems(PadToRing(_ringSpells.Select(spell => new RadialWheelItem
        {
            Label = spell.Name,
            Color = DamageTypeColors.GetForSpell(spell)
        })), highlighted);

        ShowSpellScreenInfo(highlighted);
    }

    // --- Favorites: the pinned wheel and each spell's favorites ring ---

    private static bool AnythingPinned =>
        SaveManager.Instance.Data.PinnedFavorites.Any(name => PlayerProfiles.Local.FavoriteNamed(name) != null);

    private static IEnumerable<FavoriteLoadout> FavoritesOf(Spell spell) =>
        SaveManager.Instance.Data.Favorites.Where(favorite => favorite.Spell == spell);

    private static bool IsAvailable(FavoriteLoadout favorite) =>
        favorite.Runes.Where(rune => rune != null).All(RuneCatalog.IsAvailable);

    private static RadialWheelItem FavoriteItem(FavoriteLoadout favorite) => new RadialWheelItem
    {
        Label = favorite.Name,
        Color = DamageTypeColors.GetForSpell(favorite.Spell),
        Enabled = IsAvailable(favorite)
    };

    private void ShowPinnedWheel()
    {
        SetScreen(Screen.PinnedWheel);
        _ringFavorites.Clear();
        _ringFavorites.AddRange(SaveManager.Instance.Data.PinnedFavorites.Select(PlayerProfiles.Local.FavoriteNamed));

        Wheel.SetItems(_ringFavorites.Select(favorite => favorite == null ? null : FavoriteItem(favorite)).ToList());
        ShowSpellScreenInfo(-1);
    }

    // Skipped for a spell with no favorites, which goes straight to a new build.
    private void ShowFavoritesRing(Spell spell, int highlighted = -1)
    {
        SetScreen(Screen.FavoritesRing);
        _favoritesRingSpell = spell;
        _ringFavorites.Clear();
        _ringFavorites.Add(null);
        _ringFavorites.AddRange(FavoritesOf(spell));

        List<RadialWheelItem> items = new List<RadialWheelItem>
        {
            new RadialWheelItem { Label = "New build", Color = _actionColor }
        };
        items.AddRange(_ringFavorites.Skip(1).Select(FavoriteItem));

        Wheel.SetItems(PadToRing(items), highlighted);
        ShowSpellScreenInfo(highlighted);
    }

    private void PlaceFavorite(FavoriteLoadout favorite)
    {
        EmitSignal(SignalName.Confirmed, favorite.Spell, new Godot.Collections.Array<Rune>(favorite.Runes.Where(rune => rune != null)));
        Close();
    }

    private void OnSpellChosen(Spell spell)
    {
        if (spell != _selectedSpell)
        {
            Array.Clear(_socketedRunes);
        }

        _selectedSpell = spell;
        ShowInscription(NextEmptySocketSlot(-1));
    }

    private void ShowSpellScreenInfo(int slot)
    {
        CostLabel.Text = "";
        CostLabel.RemoveThemeColorOverride("font_color");

        if (slot < 0)
        {
            ShowText("", "");
            return;
        }

        switch (_screen)
        {
            case Screen.ElementRing when slot == FavoritesSlot:
                ShowText("Favorites", AnythingPinned ? "Pinned favorites" : "Nothing pinned yet");
                break;

            case Screen.ElementRing:
                SpellWheelGroup group = GroupAt(slot);
                int count = KnownSpells.Count(spell => SpellWheelGroups.Of(spell) == group);
                ShowText(SpellWheelGroups.NameOf(group), count == 1 ? "1 spell" : $"{count} spells");
                break;

            case Screen.SpellRing when slot < _ringSpells.Count:
                Spell spell = _ringSpells[slot];
                int cost = ComputeManaCost(spell, Enumerable.Empty<Rune>());
                ShowText(spell.Name, spell.Description);
                SetCostText(CostLabel, $"Cost: {cost} / Mana: {_economy.Mana}", cost > _economy.Mana);
                break;

            case Screen.FavoritesRing when slot == NewBuildSlot:
                ShowText("New build", "Start from empty sockets.");
                break;

            case Screen.PinnedWheel or Screen.FavoritesRing when slot < _ringFavorites.Count && _ringFavorites[slot] != null:
                FavoriteLoadout favorite = _ringFavorites[slot];
                int favoriteCost = ComputeManaCost(favorite.Spell, favorite.Runes);
                ShowText(favorite.Name, string.Join(", ", favorite.Runes.Where(rune => rune != null).Select(rune => rune.Name)));
                SetCostText(CostLabel, $"Cost: {favoriteCost} / Mana: {_economy.Mana}", favoriteCost > _economy.Mana);
                break;

            default:
                ShowText("", "");
                break;
        }
    }

    private void ShowText(string title, string description)
    {
        TitleLabel.Text = title;
        DescriptionLabel.Text = description;
        DescriptionPanel.Visible = description != "";
    }

    // --- Ritual Inscription ---

    private static int SocketSlot(int socket) => socket * 2 + 1;

    private static int SocketAt(int slot) => RitualInscription.IsSocketSlot(slot) ? (slot - 1) / 2 : -1;

    // The first empty socket after afterSocket, wrapping around, or Place once every socket is full.
    private int NextEmptySocketSlot(int afterSocket)
    {
        for (int step = 1; step <= _socketedRunes.Length; step++)
        {
            int socket = (afterSocket + step) % _socketedRunes.Length;

            if (_socketedRunes[socket] == null)
            {
                return SocketSlot(socket);
            }
        }

        return PlaceSlot;
    }

    private void ShowInscription(int highlighted)
    {
        SetScreen(Screen.Inscription);
        Inscription.SetItems(BuildInscriptionItems(), BuildHoldSeconds(), highlighted);
        ShowInscriptionInfo(highlighted);
    }

    private List<RadialWheelItem> BuildInscriptionItems()
    {
        RadialWheelItem[] items = new RadialWheelItem[InscriptionSlots];

        items[SaveFavoriteSlot] = new RadialWheelItem { Label = "Save Favorite", Color = _actionColor };
        items[ClearAllSlot] = new RadialWheelItem
        {
            Label = "Clear All",
            Color = _actionColor,
            Enabled = _socketedRunes.Any(rune => rune != null)
        };
        items[PlaceSlot] = new RadialWheelItem { Label = "Place", Color = _placeColor };
        items[SaveTokenSlot] = new RadialWheelItem { Label = "Save as Token", Color = _actionColor, Enabled = false };

        for (int socket = 0; socket < _socketedRunes.Length; socket++)
        {
            Rune rune = _socketedRunes[socket];
            items[SocketSlot(socket)] = new RadialWheelItem
            {
                Label = rune?.Name ?? "Empty",
                Color = rune != null ? RarityColors.Get(rune.Rarity) : _emptySocketColor,
                Weight = RitualInscription.SocketWeight
            };
        }

        return items.ToList();
    }

    private float[] BuildHoldSeconds()
    {
        float[] holdSeconds = new float[InscriptionSlots];
        holdSeconds[ClearAllSlot] = ClearAllHoldSeconds;

        for (int socket = 0; socket < _socketedRunes.Length; socket++)
        {
            holdSeconds[SocketSlot(socket)] = _socketedRunes[socket] != null ? TierHoldSeconds : 0f;
        }

        return holdSeconds;
    }

    private void OnInscriptionHighlightChanged(int slot)
    {
        ShowInscriptionInfo(slot);
    }

    private void ShowInscriptionInfo(int slot)
    {
        ShowInscriptionCost(_socketedRunes);
        Inscription.TitleLabel.Text = _selectedSpell.Name;

        int socket = SocketAt(slot);

        Inscription.DescriptionLabel.Text = socket >= 0
            ? _socketedRunes[socket]?.Description ?? "Empty socket"
            : slot switch
            {
                SaveFavoriteSlot => "Save this build as a favorite.",
                ClearAllSlot => "Hold to remove every socketed rune.",
                PlaceSlot => "Place this ritual.",
                SaveTokenSlot => "Save this build as a token.",
                _ => ""
            };
    }

    private void ShowInscriptionCost(IEnumerable<Rune> runes)
    {
        int cost = ComputeManaCost(runes);
        SetCostText(Inscription.CostLabel, $"Cost: {cost} / Mana: {_economy.Mana}", cost > _economy.Mana);
    }

    private void OnInscriptionTapped(int slot)
    {
        int socket = SocketAt(slot);

        if (socket >= 0)
        {
            _socketBeingAssigned = socket;
            ShowBucketRing();
            return;
        }

        switch (slot)
        {
            case PlaceSlot:
                EmitSignal(SignalName.Confirmed, _selectedSpell, new Godot.Collections.Array<Rune>(_socketedRunes.Where(rune => rune != null)));
                Close();
                break;

            case SaveFavoriteSlot:
                FavoriteLoadout existing = PlayerProfiles.Local.FindFavorite(_selectedSpell, _socketedRunes);

                Inscription.DescriptionLabel.Text = existing != null
                    ? $"Already saved as \"{existing.Name}\"."
                    : $"Saved as \"{PlayerProfiles.Local.AddFavorite(FavoriteLoadout.DefaultName(_selectedSpell, _socketedRunes), _selectedSpell, _socketedRunes)}\".";
                break;

            case ClearAllSlot:
                Inscription.DescriptionLabel.Text = "Hold to clear every rune.";
                break;
        }
    }

    private void OnInscriptionHeld(int slot)
    {
        int socket = SocketAt(slot);

        if (socket >= 0)
        {
            _socketBeingAssigned = socket;
            ShowTierWheel();
            return;
        }

        if (slot == ClearAllSlot)
        {
            Array.Clear(_socketedRunes);
            Inscription.ReplaceItems(BuildInscriptionItems(), BuildHoldSeconds());
            ShowInscriptionInfo(slot);
        }
    }

    // --- Rune wheel: bucket ring, rune ring and tier wheel, all around the socket being filled ---

    private void PlaceRuneWheelAround(int socket)
    {
        Vector2 center = RitualInscription.SlotOffset(SocketSlot(socket));
        float innerRadius = RitualInscription.SocketDiameter / 2f + RuneRingGap;
        float outerRadius = innerRadius + RuneRingThickness;

        RuneWheel.OffsetLeft = center.X - outerRadius;
        RuneWheel.OffsetRight = center.X + outerRadius;
        RuneWheel.OffsetTop = center.Y - outerRadius;
        RuneWheel.OffsetBottom = center.Y + outerRadius;
        RuneWheel.InnerRadiusFraction = innerRadius / outerRadius;
        PlaceRuneInfoPanel(socket);
    }

    private void PlaceRuneInfoPanel(int socket)
    {
        bool above = RitualInscription.SlotOffset(SocketSlot(socket)).Y < 0f;

        RuneInfoPanel.OffsetTop = above ? -RuneInfoFarEdge : RuneInfoNearEdge;
        RuneInfoPanel.OffsetBottom = above ? -RuneInfoNearEdge : RuneInfoFarEdge;
        RuneInfoPanel.GrowVertical = above ? GrowDirection.Begin : GrowDirection.End;
    }

    private static string BucketName(Tag bucket) => bucket == Tag.AoE ? "Area" : bucket.ToString();

    private IEnumerable<Rune> CompatibleRunesIn(Tag bucket) => OwnedRunes
        .Where(rune => rune.Tags.Contains(bucket) && rune.IsCompatibleWith(_selectedSpell.Tags))
        .OrderBy(rune => rune.Name);

    private void ShowBucketRing(int highlighted = -1)
    {
        SetScreen(Screen.BucketRing);
        PlaceRuneWheelAround(_socketBeingAssigned);

        RuneWheel.SetItems(RuneTags.Buckets.Select(bucket => new RadialWheelItem
        {
            Label = BucketName(bucket),
            Color = _bucketColor,
            Enabled = CompatibleRunesIn(bucket).Any()
        }).ToList(), highlighted);

        ShowRuneScreenInfo(highlighted);
    }

    private void ShowRuneRing(Tag bucket)
    {
        SetScreen(Screen.RuneRing);
        PlaceRuneWheelAround(_socketBeingAssigned);
        _openBucket = bucket;
        _ringRunes.Clear();
        _ringRunes.AddRange(CompatibleRunesIn(bucket));

        Rune current = _socketedRunes[_socketBeingAssigned];
        int highlighted = current == null ? -1 : _ringRunes.FindIndex(rune => rune.GetType() == current.GetType());

        RuneWheel.SetItems(PadToRing(_ringRunes.Select(rune => new RadialWheelItem
        {
            Label = rune.Name,
            Color = RarityColors.Get(rune.Rarity),
            Enabled = !ConflictsWithOtherSocket(rune, _socketBeingAssigned) && RuneCatalog.IsAvailable(rune)
        })), highlighted);

        ShowRuneScreenInfo(highlighted);
    }

    private void SocketRune(Rune rune)
    {
        _socketedRunes[_socketBeingAssigned] = rune;
        ShowInscription(NextEmptySocketSlot(_socketBeingAssigned));
    }

    private void ShowTierWheel()
    {
        SetScreen(Screen.TierWheel);
        PlaceRuneWheelAround(_socketBeingAssigned);
        Rune current = _socketedRunes[_socketBeingAssigned];
        List<Rune> owned = KnownRunes.Where(rune => rune.GetType() == current.GetType()).ToList();

        RadialWheelItem[] items = new RadialWheelItem[TierWheelSlots];
        items[SwapSlot] = new RadialWheelItem { Label = "Swap", Color = _actionColor };
        items[ClearSocketSlot] = new RadialWheelItem { Label = "Clear", Color = _clearColor };

        for (int i = 0; i < _runeTiers.Length; i++)
        {
            _tierRunes[i] = owned.FirstOrDefault(rune => rune.Rarity == _runeTiers[i]);
            items[FirstTierSlot + i] = new RadialWheelItem
            {
                Label = _runeTiers[i].ToString(),
                Color = RarityColors.Get(_runeTiers[i]),
                Enabled = _tierRunes[i] != null
            };
        }

        int highlighted = FirstTierSlot + Array.IndexOf(_runeTiers, current.Rarity);
        RuneWheel.SetItems(items, highlighted);
        ShowRuneScreenInfo(highlighted);
    }

    private void OnTierWheelCommitted(int slot)
    {
        switch (slot)
        {
            case SwapSlot:
                ShowBucketRing();
                return;

            case ClearSocketSlot:
                _socketedRunes[_socketBeingAssigned] = null;
                break;

            default:
                _socketedRunes[_socketBeingAssigned] = _tierRunes[slot - FirstTierSlot];
                break;
        }

        ShowInscription(SocketSlot(_socketBeingAssigned));
    }

    private void ShowRuneScreenInfo(int slot)
    {
        switch (_screen)
        {
            case Screen.BucketRing when slot >= 0:
                Tag bucket = RuneTags.Buckets[slot];
                int runeCount = CompatibleRunesIn(bucket).Count();
                ShowRunePanel(BucketName(bucket), "", runeCount == 1 ? "1 rune" : $"{runeCount} runes");
                PreviewSocket(null);
                break;

            case Screen.RuneRing when slot >= 0 && slot < _ringRunes.Count:
                ShowRuneInfo(_ringRunes[slot]);
                break;

            case Screen.TierWheel when slot == SwapSlot:
                ShowRunePanel("Swap", "", "Choose a different rune for this socket.");
                PreviewSocket(null);
                break;

            case Screen.TierWheel when slot == ClearSocketSlot:
                ShowRunePanel("Clear", "", "Remove this rune.");
                PreviewSocket(null);
                break;

            case Screen.TierWheel when slot >= FirstTierSlot && slot < FirstTierSlot + _runeTiers.Length:
                Rune tier = _tierRunes[slot - FirstTierSlot];

                if (tier != null)
                {
                    ShowRuneInfo(tier);
                }
                else
                {
                    ShowRunePanel(_runeTiers[slot - FirstTierSlot].ToString(), "", "Not owned.");
                    PreviewSocket(null);
                }

                break;

            default:
                ShowRunePanel("", "", "");
                PreviewSocket(null);
                break;
        }
    }

    private void ShowRuneInfo(Rune rune)
    {
        int costDelta = ComputeManaCost(WithSocket(_socketBeingAssigned, rune)) - ComputeManaCost(WithSocket(_socketBeingAssigned, null));
        ShowRunePanel(rune.Name, $"+{costDelta} mana", rune.Description);
        PreviewSocket(rune);
    }

    private void ShowRunePanel(string title, string cost, string description)
    {
        RuneTitleLabel.Text = title;
        RuneCostLabel.Text = cost;
        RuneDescriptionLabel.Text = description;
        RuneInfoPanel.Visible = title != "";
    }

    // The socket being filled shows what's pointed at, and the inscription's total follows it. Null
    // shows what the socket actually holds.
    private void PreviewSocket(Rune candidate)
    {
        Rune shown = candidate ?? _socketedRunes[_socketBeingAssigned];
        Inscription.PreviewSocketLabel(SocketSlot(_socketBeingAssigned), shown?.Name ?? "Empty");
        ShowInscriptionCost(candidate != null ? WithSocket(_socketBeingAssigned, candidate) : _socketedRunes);
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
                PlaceFavorite(_ringFavorites[slot]);
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

            case Screen.RuneRing:
                SocketRune(_ringRunes[slot]);
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

    // --- Costs and rune rules ---

    private int ComputeManaCost(IEnumerable<Rune> runes) => ComputeManaCost(_selectedSpell, runes);

    private static int ComputeManaCost(Spell spell, IEnumerable<Rune> runes) =>
        SpellInstance.ResolvePreview(spell, runes.Where(rune => rune != null), PlayerProfiles.Local.SelectedSheet).ResolvedManaCost;

    private Rune[] WithSocket(int socket, Rune rune)
    {
        Rune[] result = (Rune[])_socketedRunes.Clone();
        result[socket] = rune;
        return result;
    }

    private bool ConflictsWithOtherSocket(Rune rune, int socket)
    {
        for (int i = 0; i < _socketedRunes.Length; i++)
        {
            if (i != socket && _socketedRunes[i] != null && _socketedRunes[i].ConflictsWith(rune))
            {
                return true;
            }
        }

        return false;
    }

    // --- Backing out and closing ---

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
        switch (_screen)
        {
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
            case Screen.Inscription when FavoritesOf(_selectedSpell).Any():
                ShowFavoritesRing(_selectedSpell, NewBuildSlot);
                break;

            case Screen.Inscription:
                ShowSpellRing(SpellWheelGroups.Of(_selectedSpell), _selectedSpell);
                break;

            case Screen.BucketRing:
            case Screen.TierWheel:
                ShowInscription(SocketSlot(_socketBeingAssigned));
                break;

            case Screen.RuneRing:
                ShowBucketRing(RuneTags.Buckets.ToList().IndexOf(_openBucket));
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
        Visible = false;
        UiPanelStack.Pop(this);
        EmitSignal(SignalName.Closed);
    }
}
