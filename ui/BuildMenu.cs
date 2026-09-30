using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// The Architect's construction menu: a radial wheel of the player's known spells, grouped by element
// on the first ring. Committing a spell hands it to placement.
public partial class BuildMenu : Control
{
    [Signal] public delegate void ClosedEventHandler();
    [Signal] public delegate void ConfirmedEventHandler(Spell spell, Godot.Collections.Array<Rune> runes);

    [Export] public RadialWheel Wheel { get; private set; }
    [Export] public Label TitleLabel { get; private set; }
    [Export] public Label CostLabel { get; private set; }
    [Export] public Label DescriptionLabel { get; private set; }
    [Export] public Control DescriptionPanel { get; private set; }

    // A spell ring always has at least this many slots, so learning a spell fills a gap instead of
    // reshaping the ring. An element with more than this grows the ring until the spiral exists.
    private const int MinSpellSlots = 8;

    // The element ring's top wedge; the element groups follow it clockwise.
    private const int FavoritesSlot = 0;

    private static readonly SpellWheelGroup[] _groups = Enum.GetValues<SpellWheelGroup>();
    private static readonly Color _favoritesColor = new Color(1f, 0.8f, 0.3f);
    private static readonly Color _unaffordableColor = new Color(1f, 0.35f, 0.35f);

    // Null while the element ring is showing.
    private SpellWheelGroup? _openGroup;
    private readonly List<Spell> _ringSpells = new();

    private EconomyManager _economy;

    private static IEnumerable<Spell> KnownSpells => SaveManager.Instance.Data.KnownSpells;

    public override void _Ready()
    {
        _economy = EconomyManager.Instance;
        Wheel.HighlightChanged += OnHighlightChanged;
        Wheel.Committed += OnCommitted;
    }

    public override void _Process(double delta)
    {
        Wheel.AcceptsInput = Visible && UiPanelStack.IsTop(this);
    }

    public void Open()
    {
        Visible = true;
        UiPanelStack.Push(this, Close, capturesMouse: true);
        ShowElementRing();
    }

    private void ShowElementRing(int highlighted = -1)
    {
        _openGroup = null;
        _ringSpells.Clear();

        List<RadialWheelItem> items = new List<RadialWheelItem>
        {
            new RadialWheelItem { Label = "Favorites", Color = _favoritesColor, Enabled = false }
        };

        items.AddRange(_groups.Select(group => new RadialWheelItem
        {
            Label = SpellWheelGroups.NameOf(group),
            Color = SpellWheelGroups.ColorOf(group),
            Enabled = KnownSpells.Any(spell => SpellWheelGroups.Of(spell) == group)
        }));

        Wheel.SetItems(items, highlighted);
        ShowInfoFor(highlighted);
    }

    private static SpellWheelGroup GroupAt(int slot) => _groups[slot - FavoritesSlot - 1];

    private static int SlotOf(SpellWheelGroup group) => Array.IndexOf(_groups, group) + FavoritesSlot + 1;

    private void ShowSpellRing(SpellWheelGroup group)
    {
        _openGroup = group;
        _ringSpells.Clear();
        _ringSpells.AddRange(KnownSpells
            .Where(spell => SpellWheelGroups.Of(spell) == group)
            .OrderBy(spell => spell.WheelOrder)
            .ThenBy(spell => spell.Name));

        int slotCount = Math.Max(MinSpellSlots, _ringSpells.Count);
        List<RadialWheelItem> items = new List<RadialWheelItem>(slotCount);

        for (int i = 0; i < slotCount; i++)
        {
            items.Add(i < _ringSpells.Count
                ? new RadialWheelItem { Label = _ringSpells[i].Name, Color = DamageTypeColors.GetForSpell(_ringSpells[i]) }
                : null);
        }

        Wheel.SetItems(items);
        ShowInfoFor(-1);
    }

    private void OnHighlightChanged(int slot)
    {
        ShowInfoFor(slot);
    }

    private void ShowInfoFor(int slot)
    {
        CostLabel.Text = "";
        CostLabel.RemoveThemeColorOverride("font_color");

        if (slot < 0)
        {
            ShowText("", "");
            return;
        }

        if (!_openGroup.HasValue)
        {
            if (slot == FavoritesSlot)
            {
                ShowText("Favorites", "Pinned favorites");
                return;
            }

            SpellWheelGroup group = GroupAt(slot);
            int count = KnownSpells.Count(spell => SpellWheelGroups.Of(spell) == group);
            ShowText(SpellWheelGroups.NameOf(group), count == 1 ? "1 spell" : $"{count} spells");
            return;
        }

        if (slot >= _ringSpells.Count)
        {
            ShowText("", "");
            return;
        }

        Spell spell = _ringSpells[slot];
        int cost = SpellInstance.ResolvePreview(spell, Enumerable.Empty<Rune>(), PlayerProfiles.Local.SelectedSheet).ResolvedManaCost;
        ShowText(spell.Name, spell.Description);
        CostLabel.Text = $"Cost: {cost} / Mana: {_economy.Mana}";

        if (cost > _economy.Mana)
        {
            CostLabel.AddThemeColorOverride("font_color", _unaffordableColor);
        }
    }

    private void ShowText(string title, string description)
    {
        TitleLabel.Text = title;
        DescriptionLabel.Text = description;
        DescriptionPanel.Visible = description != "";
    }

    private void OnCommitted(int slot)
    {
        if (!_openGroup.HasValue)
        {
            // The pinned favorites wheel arrives with favorites; until then its wedge can't be committed.
            if (slot != FavoritesSlot)
            {
                ShowSpellRing(GroupAt(slot));
            }

            return;
        }

        EmitSignal(SignalName.Confirmed, _ringSpells[slot], new Godot.Collections.Array<Rune>());
        Close();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !UiPanelStack.IsTop(this))
        {
            return;
        }

        // The key that opened the menu closes it from any ring; the others step back one ring.
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
        if (_openGroup.HasValue)
        {
            ShowElementRing(SlotOf(_openGroup.Value));
        }
        else
        {
            Close();
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
