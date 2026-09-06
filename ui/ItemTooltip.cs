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
// buy/craft) catches a slot's contents changing while the mouse never left it, and Alt press/
// release (real discrete key events) catches the advanced-view toggle.
//
// Position is anchored to the hovered item's own rect (left side, top-aligned), not the mouse -
// recomputed every frame while a source is hovered (harmless, since the source's rect isn't
// moving) and then simply left alone once the mouse leaves it. Holding Alt is what keeps the
// panel open after that: releasing the source no longer hides it while Alt is held, so the mouse
// can cross the gap and hover a tier badge for its nested mod-details popup without the panel
// trying to flee from an approaching cursor.
public partial class ItemTooltip : CanvasLayer
{
    private const float PanelGap = 12f;
    private const float GroupLabelWidth = 50f;
    private const float NestedCursorOffsetX = 18f;
    private const float NestedCursorOffsetY = 18f;
    private static readonly Color MutedColor = new Color(0.65f, 0.65f, 0.65f);

    public static ItemTooltip Instance { get; private set; }

    private readonly Dictionary<Control, Func<Item>> _sources = new();

    private PanelContainer _panel;
    private Label _headerLabel;
    private Label _plainBodyLabel;
    private VBoxContainer _advancedBody;
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
        _panel = new PanelContainer
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
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
        });

        VBoxContainer content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(content);

        _headerLabel = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddChild(_headerLabel);

        _plainBodyLabel = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddChild(_plainBodyLabel);

        _advancedBody = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.AddChild(_advancedBody);

        AddChild(_panel);

        _nestedPanel = new PanelContainer
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _nestedPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
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
        });

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

        if (@event.IsActionPressed("advanced_tooltip") || @event.IsActionReleased("advanced_tooltip"))
        {
            RefreshContent();
        }
    }

    public override void _Process(double delta)
    {
        if (_hoveredSource != null && IsInstanceValid(_hoveredSource))
        {
            Rect2 sourceRect = _hoveredSource.GetGlobalRect();
            _panel.Position = new Vector2(sourceRect.Position.X - _panel.Size.X - PanelGap, sourceRect.Position.Y);
        }

        if (_nestedPanel.Visible)
        {
            _nestedPanel.Position = GetViewport().GetMousePosition() + new Vector2(NestedCursorOffsetX, NestedCursorOffsetY);
        }
    }

    private void RefreshContent()
    {
        bool advanced = Input.IsActionPressed("advanced_tooltip");
        bool isShowing = _hoveredSource != null || (advanced && _currentItem != null);

        if (!isShowing)
        {
            _panel.Visible = false;
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
            _panel.Visible = false;
            return;
        }

        _headerLabel.Text = _currentItem.HeaderText;

        _plainBodyLabel.Visible = !advanced;
        _advancedBody.Visible = advanced;

        if (advanced)
        {
            BuildAdvancedBody(_currentItem);
        }
        else
        {
            _plainBodyLabel.Text = string.Join("\n", _currentItem.Affixes.Select(roll => roll.BuildDescription()));
            HideNestedPopup();
        }

        _panel.Visible = true;
    }

    private void BuildAdvancedBody(Item item)
    {
        foreach (Node child in _advancedBody.GetChildren())
        {
            child.QueueFree();
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

    // A "Prefix"/"Suffix" label in a narrow left column, vertically centered against a right
    // column stacking that group's rows (built via the same AffixRowView the crafting table's
    // bars use) - matches PoE2's tooltip layout. Each row's tier badge also gets its own hover
    // wired here, since only the tooltip (not the crafting bars) needs the nested details popup.
    private HBoxContainer BuildGroup(string label, List<RolledAffix> rolls)
    {
        HBoxContainer group = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };

        Label groupLabel = new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(GroupLabelWidth, 0f),
            VerticalAlignment = VerticalAlignment.Center,
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
            WireTierBadge(built.TierBadge, roll);
        }

        group.AddChild(rows);

        return group;
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
}
