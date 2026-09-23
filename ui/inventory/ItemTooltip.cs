using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// A hover tooltip for Items, replacing Godot's built-in Control.TooltipText mechanism entirely.
// The built-in tooltip is wrapped in an internal popup-style window that closes itself on
// ui_cancel (Escape) - which read as the tooltip flickering shut and instantly reopening the
// moment the mouse so much as twitches.
//
// Event-driven, not polled: MouseEntered/MouseExited (real Godot signals) track which registered
// control is hovered, ItemDragController.Changed (already fires on every pickup/place/swap/sell/
// buy/craft) catches a slot's contents changing while the mouse never left it, and Alt/Ctrl press/
// release (real discrete key events) catch the advanced-view and compare-to-equipped toggles.
//
// Position is anchored to the hovered item's own rect (left side, top-aligned), not the mouse -
// recomputed every frame while a source is hovered (harmless, since the source's rect isn't
// moving) and then simply left alone once the mouse leaves it. Holding Alt or Ctrl keeps the panel
// open past that point, so the mouse can cross the gap and hover a tier badge for its nested
// mod-details popup instead of the panel fleeing an approaching cursor.
//
// Ctrl (compare_item) adds up to two more panels comparing the hovered item against whatever's
// equipped in the same slot for the active Sentinel (both rings, for a Ring); Ctrl+Alt shows all
// of them in the advanced view together. compare_item is its own Input Map action, not reused
// from quick_move, even though both default to Ctrl on keyboard - a controller would likely want
// "move this item" and "hold to compare" on different buttons, since they're triggered
// completely differently (a click vs. a hold-while-hovering).
public partial class ItemTooltip : CanvasLayer
{
    private const float PanelGap = 12f;
    private const float NestedCursorOffsetX = 18f;
    private const float NestedCursorOffsetY = 18f;
    private const int MaxComparisonPanels = 2;
    private static readonly Color MutedColor = new Color(0.65f, 0.65f, 0.65f);

    public static ItemTooltip Instance { get; private set; }

    private readonly Dictionary<Control, Func<Item>> _sources = new();

    private TooltipPanelView _mainPanel;
    private readonly List<TooltipPanelView> _comparisonPanels = new();

    private PanelContainer _nestedPanel;
    private Label _nestedHeaderLabel;
    private Label _nestedTagsLabel;

    private Control _hoveredSource;
    private Item _currentItem;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        _mainPanel = new TooltipPanelView(this, WireTierBadge);

        for (int i = 0; i < MaxComparisonPanels; i++)
        {
            _comparisonPanels.Add(new TooltipPanelView(this, WireTierBadge));
        }

        _nestedPanel = new PanelContainer
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _nestedPanel.AddThemeStyleboxOverride("panel", TooltipPanelView.BuildBackgroundStyle(TooltipPanelView.DefaultBackgroundColor));

        VBoxContainer nestedContent = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _nestedPanel.AddChild(nestedContent);

        _nestedHeaderLabel = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        nestedContent.AddChild(_nestedHeaderLabel);

        HBoxContainer tagsRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        nestedContent.AddChild(tagsRow);

        Label tagsLabel = new Label { Text = "Tags -", MouseFilter = Control.MouseFilterEnum.Ignore };
        tagsLabel.AddThemeColorOverride("font_color", MutedColor);
        tagsRow.AddChild(tagsLabel);

        _nestedTagsLabel = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        tagsRow.AddChild(_nestedTagsLabel);

        AddChild(_nestedPanel);

        ItemDragController.Instance.Changed += RefreshContent;
    }

    public override void _ExitTree()
    {
        ItemDragController.Instance.Changed -= RefreshContent;
    }

    // Registers a control whose current item (if any) should show as a tooltip while hovered -
    // looked up live via getItem, not captured once at registration time. Prunes dead keys first:
    // this is an autoload and outlives every hub-scene panel, the same staleness
    // ItemDragController's registries already had to guard against.
    public void Register(Control control, Func<Item> getItem)
    {
        PruneDeadSources();
        _sources[control] = getItem;

        control.MouseEntered += () => OnSourceMouseEntered(control);
        control.MouseExited += () => OnSourceMouseExited(control);
    }

    private void PruneDeadSources()
    {
        List<Control> deadKeys = new();

        foreach (Control key in _sources.Keys)
        {
            if (!IsInstanceValid(key))
            {
                deadKeys.Add(key);
            }
        }

        foreach (Control key in deadKeys)
        {
            _sources.Remove(key);
        }
    }

    private void OnSourceMouseEntered(Control control)
    {
        _hoveredSource = control;
        RefreshContent();
    }

    // Guarded by "is this still the hovered control" in case an out-of-order Exited (for a
    // control the mouse already left) arrives after a newer Entered - shouldn't normally happen,
    // but costs nothing to guard against clobbering a genuinely-still-hovered control.
    private void OnSourceMouseExited(Control control)
    {
        if (_hoveredSource != control)
        {
            return;
        }

        _hoveredSource = null;
        RefreshContent();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_hoveredSource == null && _currentItem == null)
        {
            return;
        }

        bool relevant = @event.IsActionPressed("advanced_tooltip") || @event.IsActionReleased("advanced_tooltip")
            || @event.IsActionPressed("compare_item") || @event.IsActionReleased("compare_item");

        if (relevant)
        {
            RefreshContent();
        }
    }

    public override void _Process(double delta)
    {
        UpdatePositions();

        if (_nestedPanel.Visible)
        {
            _nestedPanel.Position = GetViewport().GetMousePosition() + new Vector2(NestedCursorOffsetX, NestedCursorOffsetY);
        }
    }

    private void UpdatePositions()
    {
        if (_hoveredSource == null || !IsInstanceValid(_hoveredSource))
        {
            return;
        }

        Rect2 sourceRect = _hoveredSource.GetGlobalRect();

        // Anchoring to the left assumes there's room to the left - true for Inventory (right 40%
        // of the screen) but not for Crafting/Vendor (left 40%), where it would render off-screen
        // entirely. Flip to the right side of the source whenever the left doesn't actually fit -
        // checked against the full leftward extent (main panel plus the widest visible comparison
        // panel stacked further out), not just the main panel alone, so a comparison panel hanging
        // off-screen flips everything together instead of only the piece that doesn't fit.
        float leftExtent = _mainPanel.Size.X;
        float maxComparisonWidth = 0f;
        bool anyComparisonVisible = false;
        float comparisonStackHeight = 0f;

        foreach (TooltipPanelView panel in _comparisonPanels)
        {
            if (!panel.Visible)
            {
                continue;
            }

            anyComparisonVisible = true;
            maxComparisonWidth = Mathf.Max(maxComparisonWidth, panel.Size.X);
            comparisonStackHeight += panel.Size.Y + PanelGap;
        }

        if (anyComparisonVisible)
        {
            leftExtent += PanelGap + maxComparisonWidth;
            comparisonStackHeight -= PanelGap;
        }

        bool fitsOnLeft = sourceRect.Position.X - leftExtent - PanelGap >= 0f;
        float mainX = fitsOnLeft
            ? sourceRect.Position.X - _mainPanel.Size.X - PanelGap
            : sourceRect.Position.X + sourceRect.Size.X + PanelGap;

        // Clamp the whole stack (main panel, or the taller of main/comparison-stack) to the
        // viewport height - a Ring showing two stacked comparison panels is the case that
        // actually overflows.
        float totalHeight = Mathf.Max(_mainPanel.Size.Y, comparisonStackHeight);
        float viewportHeight = GetViewport().GetVisibleRect().Size.Y;
        float maxY = Mathf.Max(0f, viewportHeight - totalHeight);
        float mainY = Mathf.Clamp(sourceRect.Position.Y, 0f, maxY);

        Vector2 mainPosition = new Vector2(mainX, mainY);
        _mainPanel.SetPosition(mainPosition);

        float y = mainPosition.Y;

        foreach (TooltipPanelView panel in _comparisonPanels)
        {
            if (!panel.Visible)
            {
                continue;
            }

            float panelX = fitsOnLeft
                ? mainPosition.X - PanelGap - panel.Size.X
                : mainPosition.X + _mainPanel.Size.X + PanelGap;

            panel.SetPosition(new Vector2(panelX, y));
            y += panel.Size.Y + PanelGap;
        }
    }

    private void RefreshContent()
    {
        bool advanced = Input.IsActionPressed("advanced_tooltip");
        bool compare = Input.IsActionPressed("compare_item");
        bool isShowing = _hoveredSource != null || ((advanced || compare) && _currentItem != null);

        if (!isShowing)
        {
            _mainPanel.Hide();
            HideComparisonPanels();
            _currentItem = null;
            HideNestedPopup();
            return;
        }

        if (_hoveredSource != null)
        {
            _currentItem = ResolveSourceItem();
        }

        if (_currentItem == null)
        {
            _mainPanel.Hide();
            HideComparisonPanels();
            return;
        }

        _mainPanel.Show(_currentItem, advanced);

        if (!advanced)
        {
            HideNestedPopup();
        }

        if (compare)
        {
            ShowComparisonPanels(_currentItem, advanced);
        }
        else
        {
            HideComparisonPanels();
        }
    }

    private void ShowComparisonPanels(Item item, bool advanced)
    {
        List<Item> comparisons = GetComparisonItems(item);

        for (int i = 0; i < _comparisonPanels.Count; i++)
        {
            if (i < comparisons.Count)
            {
                _comparisonPanels[i].Show(comparisons[i], advanced, isEquipped: true);
            }
            else
            {
                _comparisonPanels[i].Hide();
            }
        }
    }

    private void HideComparisonPanels()
    {
        foreach (TooltipPanelView panel in _comparisonPanels)
        {
            panel.Hide();
        }
    }

    // Whatever's currently equipped in the hovered item's own slot, for the active Sentinel - both
    // rings for a Ring, excluding the hovered item itself (comparing it to itself is pointless,
    // e.g. hovering something you already have equipped).
    private static List<Item> GetComparisonItems(Item item)
    {
        CharacterId activeCharacter = PlayerProfiles.Local.SelectedCharacter;
        EquippedLoadout loadout = SaveManager.Instance.Data.GetOrCreateLoadout(activeCharacter);

        return loadout.GetEquipped(item.Slot).Where(equipped => equipped != item).ToList();
    }

    private void WireTierBadge(Control badge, RolledAffix roll)
    {
        badge.MouseEntered += () => ShowNestedPopup(roll);
        badge.MouseExited += HideNestedPopup;
    }

    private void ShowNestedPopup(RolledAffix roll)
    {
        _nestedHeaderLabel.Text = roll.BuildModHeaderText();
        _nestedTagsLabel.Text = string.Join(", ", roll.Affix.Tags);
        _nestedPanel.Visible = true;
    }

    private void HideNestedPopup()
    {
        _nestedPanel.Visible = false;
    }

    // Suppressed while something's actively held. IsInstanceValid guards against _hoveredSource
    // referencing a control freed without ever firing MouseExited - e.g. leaving the hub scene
    // entirely while a slot happened to be hovered.
    private Item ResolveSourceItem()
    {
        if (_hoveredSource == null || !IsInstanceValid(_hoveredSource) || ItemDragController.Instance.Held != null)
        {
            return null;
        }

        return _sources.TryGetValue(_hoveredSource, out Func<Item> getItem) ? getItem() : null;
    }

    // Combined bounding rect of whatever tooltip panel(s) are currently visible - lets other
    // world-space UI (the ground-item panel) avoid overlapping this.
    public Rect2? GetOccupiedScreenRect()
    {
        Rect2? combined = null;

        void Include(bool visible, Vector2 position, Vector2 size)
        {
            if (!visible)
            {
                return;
            }

            Rect2 rect = new Rect2(position, size);
            combined = combined.HasValue ? combined.Value.Merge(rect) : rect;
        }

        Include(_mainPanel.Visible, _mainPanel.Position, _mainPanel.Size);

        foreach (TooltipPanelView panel in _comparisonPanels)
        {
            Include(panel.Visible, panel.Position, panel.Size);
        }

        Include(_nestedPanel.Visible, _nestedPanel.Position, _nestedPanel.Size);

        return combined;
    }
}
