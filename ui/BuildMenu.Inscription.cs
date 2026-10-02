using Godot;
using System.Collections.Generic;
using System.Linq;

// The Ritual Inscription: the spell at the center, the four sockets on the diagonals, and the
// build's actions on the cardinals.
public partial class BuildMenu
{
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

    // Holding Save Favorite names it before saving; a tap saves under the default name.
    private const float NameFavoriteHoldSeconds = GameRules.AbilityHoldThreshold;

    private static readonly Color _placeColor = new Color(0.2f, 0.45f, 0.25f);

    private LineEdit _favoriteNameField;

    // Socket 0 is top-left, then clockwise. Wheel slots count clockwise from the top, so the sockets
    // sit on the odd slots and top-left is the last of them.
    private static int SocketSlot(int socket) => (socket * 2 + InscriptionSlots - 1) % InscriptionSlots;

    private static int SocketAt(int slot) => RitualInscription.IsSocketSlot(slot) ? (slot + 1) / 2 % 4 : -1;

    // The first empty socket after afterSocket, wrapping around, or Place once every socket is full.
    private int NextEmptySocketSlot(int afterSocket)
    {
        int socket = _build.NextEmptySocket(afterSocket);
        return socket >= 0 ? SocketSlot(socket) : PlaceSlot;
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
            Enabled = !_build.IsEmpty
        };
        items[PlaceSlot] = new RadialWheelItem { Label = IsAuthoringToken ? "Save Token" : "Place", Color = _placeColor };
        items[SaveTokenSlot] = IsAuthoringToken
            ? new RadialWheelItem { Label = "Clear Token", Color = _clearColor, Enabled = SavedTokens[_authoringToken] != null }
            : new RadialWheelItem { Label = "Save as Token", Color = _actionColor };

        for (int socket = 0; socket < _build.SocketCount; socket++)
        {
            Rune rune = _build.RuneIn(socket);
            items[SocketSlot(socket)] = new RadialWheelItem
            {
                Label = rune?.Name ?? "Empty",
                Color = rune != null ? RarityColors.Get(rune.Rarity) : _emptySocketColor
            };
        }

        return items.ToList();
    }

    private float[] BuildHoldSeconds()
    {
        float[] holdSeconds = new float[InscriptionSlots];
        holdSeconds[SaveFavoriteSlot] = NameFavoriteHoldSeconds;
        holdSeconds[ClearAllSlot] = ClearAllHoldSeconds;
        holdSeconds[SaveTokenSlot] = IsAuthoringToken ? ClearTokenHoldSeconds : 0f;

        for (int socket = 0; socket < _build.SocketCount; socket++)
        {
            holdSeconds[SocketSlot(socket)] = _build.RuneIn(socket) != null ? TierHoldSeconds : 0f;
        }

        return holdSeconds;
    }

    private void OnInscriptionHighlightChanged(int slot)
    {
        ShowInscriptionInfo(slot);
    }

    private void ShowInscriptionInfo(int slot)
    {
        ShowInscriptionCost(_build.ManaCost);
        Inscription.TitleLabel.Text = _build.Spell.Name;

        int socket = SocketAt(slot);

        Inscription.DescriptionLabel.Text = socket >= 0
            ? _build.RuneIn(socket)?.Description ?? "Empty socket"
            : slot switch
            {
                SaveFavoriteSlot => "Save this build as a favorite.",
                ClearAllSlot => "Hold to remove every socketed rune.",
                PlaceSlot when IsAuthoringToken => $"Save this build to token {_authoringToken + 1}.",
                PlaceSlot => "Place this ritual.",
                SaveTokenSlot when IsAuthoringToken => "Hold to empty this token.",
                SaveTokenSlot => "Save this build as a token.",
                _ => ""
            };
    }

    private void ShowInscriptionCost(int cost)
    {
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
            case PlaceSlot when IsAuthoringToken:
                FinishToken(TokenFrom(_build.Spell, _build.Sockets));
                break;

            case PlaceSlot:
                EmitSignal(SignalName.Confirmed, _build.Spell, new Godot.Collections.Array<Rune>(_build.Runes));
                Close();
                break;

            case SaveFavoriteSlot:
                FavoriteLoadout existing = PlayerProfiles.Local.FindFavorite(_build.Spell, _build.Sockets);

                Inscription.DescriptionLabel.Text = existing != null
                    ? $"Already saved as \"{existing.Name}\"."
                    : $"Saved as \"{PlayerProfiles.Local.AddFavorite(FavoriteLoadout.DefaultName(_build.Spell, _build.Sockets), _build.Spell, _build.Sockets)}\".";
                break;

            case ClearAllSlot:
                Inscription.DescriptionLabel.Text = "Hold to clear every rune.";
                break;

            case SaveTokenSlot when IsAuthoringToken:
                Inscription.DescriptionLabel.Text = "Hold to empty this token.";
                break;

            case SaveTokenSlot:
                ShowTokenWheel(savesBuild: true);
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
            _build.Clear();
            Inscription.ReplaceItems(BuildInscriptionItems(), BuildHoldSeconds());
            ShowInscriptionInfo(slot);
        }
        else if (slot == SaveTokenSlot && IsAuthoringToken)
        {
            FinishToken(null);
        }
        else if (slot == SaveFavoriteSlot)
        {
            BeginNamingFavorite();
        }
    }

    private void BeginNamingFavorite()
    {
        FavoriteLoadout existing = PlayerProfiles.Local.FindFavorite(_build.Spell, _build.Sockets);

        if (existing != null)
        {
            Inscription.DescriptionLabel.Text = $"Already saved as \"{existing.Name}\".";
            return;
        }

        _favoriteNameField = new LineEdit
        {
            Text = FavoriteLoadout.DefaultName(_build.Spell, _build.Sockets),
            MaxLength = 32,
            CustomMinimumSize = new Vector2(320f, 0f)
        };
        Inscription.DescriptionLabel.AddSibling(_favoriteNameField);

        LineEdit field = _favoriteNameField;
        field.TextSubmitted += SubmitFavoriteName;
        field.GuiInput += @event =>
        {
            if (@event.IsActionPressed("ui_cancel"))
            {
                field.AcceptEvent();
                EndNamingFavorite();
                ShowInscriptionInfo(SaveFavoriteSlot);
            }
        };

        Inscription.AcceptsInput = false;
        field.GrabFocus();
        field.SelectAll();
        Inscription.DescriptionLabel.Text = "Type a name and press Enter. Escape cancels.";
    }

    private void SubmitFavoriteName(string text)
    {
        string name = text.StripEdges();
        string saved = PlayerProfiles.Local.AddFavorite(
            name == "" ? FavoriteLoadout.DefaultName(_build.Spell, _build.Sockets) : name, _build.Spell, _build.Sockets);

        EndNamingFavorite();
        Inscription.DescriptionLabel.Text = $"Saved as \"{saved}\".";
    }

    private void EndNamingFavorite()
    {
        if (_favoriteNameField == null)
        {
            return;
        }

        _favoriteNameField.QueueFree();
        _favoriteNameField = null;
        Inscription.AcceptsInput = true;
    }
}
