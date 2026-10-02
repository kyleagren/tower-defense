using Godot;
using System.Collections.Generic;
using System.Linq;

// The Favorites tab: each spell's favorites to reorder, rename, pin and delete, beside the pinned
// favorites wheel's eight slots, laid out in the directions they take on the wheel.
public partial class OrganizePanel
{
    // Each grid cell's pinned-wheel slot, row by row: the wheel's slot 0 is at the top and they run
    // clockwise. The center cell holds none.
    private static readonly int[] _slotAtCell = { 7, 0, 1, 6, -1, 2, 5, 4, 3 };

    private static readonly Vector2 _slotSize = new Vector2(220f, 80f);
    private static readonly Color _heldSlotTint = new Color(1f, 0.85f, 0.4f);

    // A first press on Delete arms it; it disarms on its own after this long.
    private const double DeleteConfirmSeconds = 3.0;

    private VBoxContainer _favoriteSpells;
    private Label _favoritesTitle;
    private ReorderList _favoriteList;
    private readonly Button[] _slotButtons = new Button[GameRules.PinnedFavoriteSlots];
    private Label _favoritesStatus;

    private Spell _openSpell;
    private readonly List<FavoriteLoadout> _listedFavorites = new();

    // The favorite waiting for a slot to be pinned into.
    private FavoriteLoadout _pinning;

    // The pinned slot picked up to swap with another, or -1.
    private int _heldSlot = -1;

    private FavoriteLoadout _confirmingDelete;
    private Button _confirmingDeleteButton;

    private FavoriteLoadout _renaming;
    private Button _renamingEntry;
    private LineEdit _renameField;

    private Control BuildFavoritesPage()
    {
        VBoxContainer page = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 12);

        HBoxContainer columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 24);
        page.AddChild(columns);

        ScrollContainer spellScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(260f, 0f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true
        };
        columns.AddChild(spellScroll);

        _favoriteSpells = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _favoriteSpells.AddThemeConstantOverride("separation", 8);
        spellScroll.AddChild(_favoriteSpells);

        VBoxContainer listColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        listColumn.AddThemeConstantOverride("separation", 8);
        columns.AddChild(listColumn);

        _favoritesTitle = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        listColumn.AddChild(_favoritesTitle);

        ScrollContainer listScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true
        };
        listColumn.AddChild(listScroll);

        _favoriteList = new ReorderList { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _favoriteList.Moved += OnFavoriteMoved;
        listScroll.AddChild(_favoriteList);

        VBoxContainer wheelColumn = new VBoxContainer();
        wheelColumn.AddThemeConstantOverride("separation", 8);
        columns.AddChild(wheelColumn);

        wheelColumn.AddChild(new Label { Text = "Pinned wheel" });

        GridContainer grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        wheelColumn.AddChild(grid);

        foreach (int slot in _slotAtCell)
        {
            if (slot < 0)
            {
                grid.AddChild(new Control { CustomMinimumSize = _slotSize });
                continue;
            }

            Button button = new Button { CustomMinimumSize = _slotSize, ClipText = true };
            button.Pressed += () => OnSlotPressed(slot);
            grid.AddChild(button);
            _slotButtons[slot] = button;
        }

        _favoritesStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_favoritesStatus);

        return page;
    }

    // Spells with favorites, by element and then in each element's ring order. Keeps the spell that
    // was open if it still has any.
    private void ShowFavoritesPage()
    {
        foreach (Node child in _favoriteSpells.GetChildren())
        {
            _favoriteSpells.RemoveChild(child);
            child.QueueFree();
        }

        List<Spell> spells = _groups
            .SelectMany(group => Profile.SpellRing(group))
            .Where(spell => Profile.FavoritesOf(spell).Count > 0)
            .ToList();

        foreach (Spell spell in spells)
        {
            Button button = new Button { Text = spell.Name, Alignment = HorizontalAlignment.Left };
            button.Pressed += () => ShowFavoritesOf(spell, focusList: true);
            _favoriteSpells.AddChild(button);
        }

        ShowFavoritesOf(spells.Contains(_openSpell) ? _openSpell : spells.FirstOrDefault(), focusList: false);
        RefreshSlots();
    }

    private void FocusFavoritesStart()
    {
        if (_favoriteSpells.GetChildCount() > 0)
        {
            _favoriteSpells.GetChild<Button>(0).GrabFocus();
        }
        else
        {
            _slotButtons[0].GrabFocus();
        }
    }

    private void ShowFavoritesOf(Spell spell, bool focusList)
    {
        _openSpell = spell;
        _listedFavorites.Clear();

        if (spell != null)
        {
            _listedFavorites.AddRange(Profile.FavoritesOf(spell));
        }

        _favoritesTitle.Text = spell == null
            ? "No favorites saved yet - save one from the Ritual Inscription."
            : $"{spell.Name} favorites, in ring order";

        _favoriteList.SetEntries(_listedFavorites.Select(favorite => favorite.Name), AddFavoriteActions);

        if (focusList)
        {
            _favoriteList.FocusFirst();
        }
    }

    private void FocusFavorite(FavoriteLoadout favorite)
    {
        int index = _listedFavorites.IndexOf(favorite);

        if (index >= 0)
        {
            _favoriteList.FocusEntry(index);
        }
        else
        {
            FocusFavoritesStart();
        }
    }

    private void AddFavoriteActions(int index, HBoxContainer row)
    {
        FavoriteLoadout favorite = _listedFavorites[index];

        Button rename = ActionButton("Rename");
        rename.Pressed += () => BeginRename(favorite, row);
        row.AddChild(rename);

        Button pin = ActionButton(Profile.Data.PinnedFavorites.Contains(favorite.Name) ? "Unpin" : "Pin");
        pin.Pressed += () => OnPinPressed(favorite);
        row.AddChild(pin);

        Button delete = ActionButton("Delete");
        delete.Pressed += () => OnDeletePressed(favorite, delete);
        row.AddChild(delete);
    }

    private static Button ActionButton(string text) => new Button { Text = text, CustomMinimumSize = new Vector2(140f, 0f) };

    private void OnFavoriteMoved(int from, int to)
    {
        FavoriteLoadout favorite = _listedFavorites[from];
        _listedFavorites.RemoveAt(from);
        _listedFavorites.Insert(to, favorite);
        Profile.SetFavoriteOrder(_openSpell, _listedFavorites);
    }

    // --- Renaming, in place: the entry becomes a text field until Enter or Back ---

    private void BeginRename(FavoriteLoadout favorite, HBoxContainer row)
    {
        CancelRename();

        _renaming = favorite;
        _renamingEntry = row.GetChild<Button>(0);
        _renamingEntry.Visible = false;

        _renameField = new LineEdit { Text = favorite.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(_renameField);
        row.MoveChild(_renameField, 1);

        LineEdit field = _renameField;
        field.TextSubmitted += SubmitRename;
        field.GuiInput += @event =>
        {
            if (@event.IsActionPressed("ui_cancel"))
            {
                field.AcceptEvent();
                Button entry = _renamingEntry;
                CancelRename();
                entry.GrabFocus();
            }
        };

        // Clicking or moving away from the field gives up on the rename.
        field.FocusExited += CancelRename;

        field.GrabFocus();
        field.SelectAll();
        _favoritesStatus.Text = "Type a new name and press Enter. Back cancels.";
    }

    private void SubmitRename(string text)
    {
        FavoriteLoadout favorite = _renaming;
        string name = text.Trim();

        if (!Profile.TryRenameFavorite(favorite, name))
        {
            _favoritesStatus.Text = name == "" ? "A favorite needs a name." : $"Another favorite is already named \"{name}\".";
            return;
        }

        _renaming = null;
        ShowFavoritesPage();
        FocusFavorite(favorite);
        _favoritesStatus.Text = "";
    }

    private void CancelRename()
    {
        if (_renaming == null)
        {
            return;
        }

        _renaming = null;
        _renamingEntry.Visible = true;
        _renameField.Visible = false;
        _renameField.QueueFree();
        _favoritesStatus.Text = "";
    }

    // --- Pinning, and rearranging the pinned wheel ---

    private void OnPinPressed(FavoriteLoadout favorite)
    {
        if (Profile.Data.PinnedFavorites.Contains(favorite.Name))
        {
            Profile.UnpinFavorite(favorite);
            ShowFavoritesPage();
            FocusFavorite(favorite);
            _favoritesStatus.Text = $"Unpinned \"{favorite.Name}\".";
            return;
        }

        ReleaseSlot();
        _pinning = favorite;
        _favoritesStatus.Text = $"Choose a slot on the pinned wheel for \"{favorite.Name}\". Back cancels.";
        _slotButtons[0].GrabFocus();
    }

    private void RefreshSlots()
    {
        for (int slot = 0; slot < _slotButtons.Length; slot++)
        {
            _slotButtons[slot].Text = Profile.FavoriteNamed(Profile.Data.PinnedFavorites[slot])?.Name ?? "Empty";
        }
    }

    // Choosing a slot for a favorite pins it there. Otherwise a press picks up a pinned slot, and the
    // next press swaps it with wherever that lands.
    private void OnSlotPressed(int slot)
    {
        if (_pinning != null)
        {
            FavoriteLoadout favorite = _pinning;
            _pinning = null;
            Profile.PinFavorite(favorite, slot);
            ShowFavoritesPage();
            _slotButtons[slot].GrabFocus();
            _favoritesStatus.Text = $"Pinned \"{favorite.Name}\".";
            return;
        }

        if (_heldSlot < 0)
        {
            if (Profile.FavoriteNamed(Profile.Data.PinnedFavorites[slot]) == null)
            {
                _favoritesStatus.Text = "Use Pin on a favorite to fill this slot.";
                return;
            }

            _heldSlot = slot;
            _slotButtons[slot].Modulate = _heldSlotTint;
            _favoritesStatus.Text = "Choose a slot to swap it with. Back cancels.";
            return;
        }

        int held = _heldSlot;
        ReleaseSlot();

        if (held != slot)
        {
            Profile.SwapPins(held, slot);
            RefreshSlots();
        }

        _favoritesStatus.Text = "";
    }

    private void ReleaseSlot()
    {
        if (_heldSlot < 0)
        {
            return;
        }

        _slotButtons[_heldSlot].Modulate = Colors.White;
        _heldSlot = -1;
    }

    // --- Deleting: the first press arms the button, the second deletes ---

    private void OnDeletePressed(FavoriteLoadout favorite, Button button)
    {
        if (_confirmingDelete != favorite)
        {
            DisarmDelete();
            _confirmingDelete = favorite;
            _confirmingDeleteButton = button;
            button.Text = "Confirm?";

            GetTree().CreateTimer(DeleteConfirmSeconds).Timeout += () =>
            {
                if (_confirmingDelete == favorite)
                {
                    DisarmDelete();
                }
            };
            return;
        }

        _confirmingDelete = null;
        Profile.DeleteFavorite(favorite);
        ShowFavoritesPage();
        _favoritesStatus.Text = $"Deleted \"{favorite.Name}\".";

        if (_listedFavorites.Count > 0)
        {
            _favoriteList.FocusFirst();
        }
        else
        {
            FocusFavoritesStart();
        }
    }

    private void DisarmDelete()
    {
        if (_confirmingDeleteButton != null && IsInstanceValid(_confirmingDeleteButton))
        {
            _confirmingDeleteButton.Text = "Delete";
        }

        _confirmingDelete = null;
        _confirmingDeleteButton = null;
    }

    private bool CancelFavoritesInProgress()
    {
        if (_pinning != null)
        {
            _pinning = null;
            _favoritesStatus.Text = "";
            return true;
        }

        if (_heldSlot >= 0)
        {
            ReleaseSlot();
            _favoritesStatus.Text = "";
            return true;
        }

        if (_favoriteList.IsHolding)
        {
            _favoriteList.CancelHold();
            return true;
        }

        return false;
    }
}
