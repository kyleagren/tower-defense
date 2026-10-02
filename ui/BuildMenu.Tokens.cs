using Godot;
using System.Collections.Generic;
using System.Linq;

// The token wheel, and token mode: authoring a token through the same screens as an ordinary build,
// with saving writing it to the token instead of placing it.
public partial class BuildMenu
{
    // Tokens are free to recraft but not free to rebuild, so replacing or emptying one is held to
    // confirm.
    private const float ReplaceTokenHoldSeconds = 1f;
    private const float ClearTokenHoldSeconds = 1f;

    // Rings every wheel while a token is being authored, matching the token-mode tag.
    private static readonly Color _tokenModeColor = new Color(0.85f, 0.75f, 0.15f);

    // The token being authored, or -1 while building an ordinary ritual.
    private int _authoringToken = -1;

    // Whether the token being authored was picked on the token wheel, which finishing it returns to.
    // One reached straight from 1/2/3 closes the menu instead.
    private bool _authoringFromTokenWheel;

    // The screen a token reached straight from 1/2/3 opened on; backing out of it closes the menu.
    private Screen? _closesOnBack;

    // Whether the token wheel is choosing where to save the current build, rather than which token
    // to author.
    private bool _tokenWheelSavesBuild;

    private static Godot.Collections.Array<RuneLoadout> SavedTokens => PlayerProfiles.Local.Data.Tokens;

    private bool IsAuthoringToken => _authoringToken >= 0;

    private void ShowTokenWheel(bool savesBuild, int highlightToken = -1)
    {
        if (!savesBuild)
        {
            EndTokenAuthoring();
        }

        _tokenWheelSavesBuild = savesBuild;
        SetScreen(Screen.TokenWheel);

        RadialWheelItem[] items = new RadialWheelItem[4];
        float[] holdSeconds = new float[4];

        for (int token = 0; token < GameRules.MaxTokens; token++)
        {
            RuneLoadout saved = SavedTokens[token];
            int slot = TokenWheel.SlotOf(token);

            items[slot] = new RadialWheelItem
            {
                Label = saved == null ? "Empty" : TokenName(saved),
                Color = saved == null ? _emptySocketColor : DamageTypeColors.GetForSpell(saved.Spell)
            };
            holdSeconds[slot] = savesBuild && saved != null ? ReplaceTokenHoldSeconds : 0f;
        }

        int highlighted = highlightToken >= 0 ? TokenWheel.SlotOf(highlightToken) : -1;
        Tokens.SetItems(items, holdSeconds, highlighted);
        ShowTokenInfo(highlighted);
    }

    // A token reads as the favorite holding exactly its build, if there is one, and otherwise as its
    // spell - so a token edited away from its favorite doesn't keep a name that no longer fits.
    private static RuneLoadout TokenFrom(Spell spell, IEnumerable<Rune> runes) => new RuneLoadout
    {
        Spell = spell,
        Runes = new Godot.Collections.Array<Rune>(runes),
        Name = PlayerProfiles.Local.FindFavorite(spell, runes)?.Name ?? ""
    };

    private static string TokenName(RuneLoadout token) =>
        string.IsNullOrEmpty(token.Name) ? token.Spell?.Name ?? "" : token.Name;

    private void ShowTokenInfo(int slot)
    {
        int token = slot >= 0 ? TokenWheel.TokenAt(slot) : -1;
        RuneLoadout saved = token >= 0 ? SavedTokens[token] : null;

        Tokens.TitleLabel.Text = token >= 0 ? $"Token {token + 1}" : _tokenWheelSavesBuild ? "Save to which token?" : "Tokens";
        Tokens.CostLabel.Text = "";

        if (saved == null)
        {
            Tokens.DescriptionLabel.Text = token >= 0 ? "Empty" : "";
            return;
        }

        Tokens.CostLabel.Text = $"Deploys for {RitualBuild.ComputeManaCost(saved.Spell, saved.Runes)} mana";
        Tokens.DescriptionLabel.Text = string.Join(", ", saved.Runes.Where(rune => rune != null).Select(rune => rune.Name));
    }

    private void OnTokenWheelTapped(int slot)
    {
        int token = TokenWheel.TokenAt(slot);

        if (!_tokenWheelSavesBuild)
        {
            BeginTokenAuthoring(token, fromTokenWheel: true);
        }
        else if (SavedTokens[token] == null)
        {
            SaveBuildToToken(token);
        }
        else
        {
            Tokens.DescriptionLabel.Text = "Hold to replace this token.";
        }
    }

    private void OnTokenWheelHeld(int slot)
    {
        SaveBuildToToken(TokenWheel.TokenAt(slot));
    }

    // From an ordinary build: saves it into a token and goes back to the inscription, which can still
    // place it.
    private void SaveBuildToToken(int token)
    {
        PlayerProfiles.Local.SetToken(token, TokenFrom(_build.Spell, _build.Sockets));

        ShowInscription(SaveTokenSlot);
        Inscription.DescriptionLabel.Text = $"Saved to token {token + 1}.";
    }

    private void BeginTokenAuthoring(int token, bool fromTokenWheel = false)
    {
        _authoringToken = token;
        _authoringFromTokenWheel = fromTokenWheel;
        TokenModeLabel.Text = $"Token {token + 1}";
        ShowTokenMode(true);
        RuneLoadout saved = SavedTokens[token];
        _closesOnBack = fromTokenWheel ? null : saved == null ? Screen.ElementRing : Screen.Inscription;

        if (saved == null)
        {
            ShowElementRing();
            return;
        }

        _build.Load(saved);
        ShowInscription(PlaceSlot);
    }

    private void EndTokenAuthoring()
    {
        _authoringToken = -1;
        _closesOnBack = null;
        ShowTokenMode(false);
    }

    // A gold ring around whichever wheel is up says something's different; the tag says what.
    private void ShowTokenMode(bool shown)
    {
        Color? ring = shown ? _tokenModeColor : null;
        Wheel.OutlineColor = ring;
        RuneWheel.OutlineColor = ring;
        Inscription.BackdropOutlineColor = ring;
        TokenModeTag.Visible = shown;
    }

    // Saving or emptying a token returns to the token wheel it was picked on, with that slot showing,
    // or closes the menu if it was reached straight from 1/2/3.
    private void FinishToken(RuneLoadout saved)
    {
        int token = _authoringToken;
        PlayerProfiles.Local.SetToken(token, saved);

        if (_authoringFromTokenWheel)
        {
            ShowTokenWheel(savesBuild: false, token);
        }
        else
        {
            Close();
        }
    }

    // Tokens lock in during the unpressured Planning phase, exactly when a scarce rune belongs on a
    // real tower instead, so a limited rune can never go into one.
    private static bool IsLimited(Rune rune) =>
        PlayerProfiles.Local.Data.KnownRunes.Any(entry => entry.Rune == rune && !entry.Unlimited);
}
