using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// The element ring, each element's spell ring, each spell's favorites ring, and the pinned favorites
// wheel.
public partial class BuildMenu
{
    // The element ring's top wedge; the element groups follow it clockwise.
    private const int FavoritesSlot = 0;

    // A spell's favorites ring: New build at the top, then its favorites in saved order.
    private const int NewBuildSlot = 0;

    private static readonly SpellWheelGroup[] _groups = Enum.GetValues<SpellWheelGroup>();

    private static readonly Color _favoritesColor = new Color(1f, 0.8f, 0.3f);

    private SpellWheelGroup _openGroup;
    private readonly List<Spell> _ringSpells = new();

    // What each slot of the favorites ring or pinned wheel holds; null for New build or an empty pin.
    private readonly List<FavoriteLoadout> _ringFavorites = new();
    private Spell _favoritesRingSpell;

    private static IEnumerable<Spell> KnownSpells => PlayerProfiles.Local.Data.KnownSpells;

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

    private static bool AnythingPinned =>
        PlayerProfiles.Local.Data.PinnedFavorites.Any(name => PlayerProfiles.Local.FavoriteNamed(name) != null);

    private static IEnumerable<FavoriteLoadout> FavoritesOf(Spell spell) =>
        PlayerProfiles.Local.Data.Favorites.Where(favorite => favorite.Spell == spell);

    // A favorite with a rune that's out of copies can't be placed; one with a limited rune can't go
    // into a token at all.
    private bool IsUsable(FavoriteLoadout favorite) => favorite.Runes
        .Where(rune => rune != null)
        .All(rune => RuneCatalog.IsAvailable(rune) && !(IsAuthoringToken && IsLimited(rune)));

    private RadialWheelItem FavoriteItem(FavoriteLoadout favorite) => new RadialWheelItem
    {
        Label = favorite.Name,
        Color = DamageTypeColors.GetForSpell(favorite.Spell),
        Enabled = IsUsable(favorite)
    };

    private void ShowPinnedWheel()
    {
        SetScreen(Screen.PinnedWheel);
        _ringFavorites.Clear();
        _ringFavorites.AddRange(PlayerProfiles.Local.Data.PinnedFavorites.Select(PlayerProfiles.Local.FavoriteNamed));

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

    // Straight to placement - or, while authoring a token, into the token under the favorite's name.
    private void UseFavorite(FavoriteLoadout favorite)
    {
        if (IsAuthoringToken)
        {
            FinishToken(new RuneLoadout
            {
                Spell = favorite.Spell,
                Runes = new Godot.Collections.Array<Rune>(favorite.Runes),
                Name = favorite.Name
            });
            return;
        }

        EmitSignal(SignalName.Confirmed, favorite.Spell, new Godot.Collections.Array<Rune>(favorite.Runes.Where(rune => rune != null)));
        Close();
    }

    private void OnSpellChosen(Spell spell)
    {
        _build.ChooseSpell(spell);
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
                int cost = RitualBuild.ComputeManaCost(spell, Enumerable.Empty<Rune>());
                ShowText(spell.Name, spell.Description);
                SetCostText(CostLabel, $"Cost: {cost} / Mana: {_economy.Mana}", cost > _economy.Mana);
                break;

            case Screen.FavoritesRing when slot == NewBuildSlot:
                ShowText("New build", "Start from empty sockets.");
                break;

            case Screen.PinnedWheel or Screen.FavoritesRing when slot < _ringFavorites.Count && _ringFavorites[slot] != null:
                FavoriteLoadout favorite = _ringFavorites[slot];
                int favoriteCost = RitualBuild.ComputeManaCost(favorite.Spell, favorite.Runes);
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
}
