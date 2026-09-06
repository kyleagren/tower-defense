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
// moving) and then simply left alone once the mouse leaves it. Holding Alt is what keeps the
// panel open after that: releasing the source no longer hides it while Alt (or Ctrl) is held, so
// the mouse can cross the gap and hover a tier badge for its nested mod-details popup without the
// panel trying to flee from an approaching cursor.
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
    private const float GroupLabelWidth = 50f;
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
        _nestedPanel.AddThemeStyleboxOverride("panel", TooltipPanelView.BuildBackgroundStyle());

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

        foreach (TooltipPanelView panel in _comparisonPanels)
        {
            if (!panel.Visible)
            {
                continue;
            }

            anyComparisonVisible = true;
            maxComparisonWidth = Mathf.Max(maxComparisonWidth, panel.Size.X);
        }

        if (anyComparisonVisible)
        {
            leftExtent += PanelGap + maxComparisonWidth;
        }

        bool fitsOnLeft = sourceRect.Position.X - leftExtent - PanelGap >= 0f;
        float mainX = fitsOnLeft
            ? sourceRect.Position.X - _mainPanel.Size.X - PanelGap
            : sourceRect.Position.X + sourceRect.Size.X + PanelGap;

        Vector2 mainPosition = new Vector2(mainX, sourceRect.Position.Y);
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
        CharacterId activeCharacter = PlayerSentinels.Get(PlayerSentinels.Local);
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

    // One tooltip panel's Controls and content-building logic - header, plain body, or the
    // advanced grouped-by-type body with per-mod tier badges. Multiple instances exist so the
    // same rendering logic serves both the main hovered item and its comparison item(s).
    private class TooltipPanelView
    {
        private static readonly Color EquippedLabelColor = new Color(0.6f, 0.9f, 0.6f);

        private readonly PanelContainer _panel;
        private readonly Label _equippedLabel;
        private readonly Label _headerLabel;
        private readonly Label _plainBodyLabel;
        private readonly VBoxContainer _advancedBody;
        private readonly Action<Control, RolledAffix> _wireTierBadge;

        public TooltipPanelView(CanvasLayer parent, Action<Control, RolledAffix> wireTierBadge)
        {
            _wireTierBadge = wireTierBadge;

            _panel = new PanelContainer
            {
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _panel.AddThemeStyleboxOverride("panel", BuildBackgroundStyle());

            VBoxContainer content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            content.AddThemeConstantOverride("separation", 6);
            _panel.AddChild(content);

            _equippedLabel = new Label { Text = "Equipped", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            _equippedLabel.AddThemeColorOverride("font_color", EquippedLabelColor);
            content.AddChild(_equippedLabel);

            _headerLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            content.AddChild(_headerLabel);

            _plainBodyLabel = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
            content.AddChild(_plainBodyLabel);

            _advancedBody = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            content.AddChild(_advancedBody);

            parent.AddChild(_panel);
        }

        public bool Visible => _panel.Visible;
        public Vector2 Size => _panel.Size;

        public void SetPosition(Vector2 position)
        {
            _panel.Position = position;
        }

        public void Show(Item item, bool advanced, bool isEquipped = false)
        {
            _equippedLabel.Visible = isEquipped;
            _headerLabel.Text = item.HeaderText;
            _plainBodyLabel.Visible = !advanced;
            _advancedBody.Visible = advanced;

            if (advanced)
            {
                BuildAdvancedBody(item);
            }
            else
            {
                _plainBodyLabel.Text = string.Join("\n", item.Affixes.Select(roll => roll.BuildDescription()));
            }

            _panel.Visible = true;

            // Force a fresh fit to whatever's actually shown now - see ItemTooltip's own notes on
            // why a Control's Size needs an explicit reset to shrink back down reliably.
            _panel.Size = Vector2.Zero;
        }

        public void Hide()
        {
            _panel.Visible = false;
        }

        private void BuildAdvancedBody(Item item)
        {
            // Free() immediately, not QueueFree() - see ItemTooltip's own notes on why a deferred
            // free here risks old and new rows briefly coexisting.
            foreach (Node child in _advancedBody.GetChildren())
            {
                child.Free();
            }

            List<RolledAffix> prefixes = item.Affixes.Where(roll => roll.Affix.AffixType == AffixType.Prefix).ToList();
            List<RolledAffix> suffixes = item.Affixes.Where(roll => roll.Affix.AffixType == AffixType.Suffix).ToList();

            if (prefixes.Count > 0)
            {
                _advancedBody.AddChild(BuildGroup("Prefix", prefixes));
            }

            if (suffixes.Count > 0)
            {
                _advancedBody.AddChild(BuildGroup("Suffix", suffixes));
            }
        }

        private HBoxContainer BuildGroup(string label, List<RolledAffix> rolls)
        {
            HBoxContainer group = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };

            Label groupLabel = new Label
            {
                Text = label,
                CustomMinimumSize = new Vector2(GroupLabelWidth, 0f),
                VerticalAlignment = VerticalAlignment.Top,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            group.AddChild(groupLabel);

            VBoxContainer rows = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };

            foreach (RolledAffix roll in rolls)
            {
                AffixRow built = AffixRowView.Build(roll);
                rows.AddChild(built.Root);
                _wireTierBadge(built.TierBadge, roll);
            }

            group.AddChild(rows);

            return group;
        }

        public static StyleBoxFlat BuildBackgroundStyle()
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.08f, 0.95f),
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                ContentMarginTop = 8,
                ContentMarginBottom = 8,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            };
        }
    }
}
