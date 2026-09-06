using Godot;
using System;
using System.Collections.Generic;

// A hover tooltip for Items, replacing Godot's built-in Control.TooltipText mechanism entirely.
// The built-in tooltip is wrapped in an internal popup-style window that closes itself on
// ui_cancel (Escape) - which read as the tooltip flickering shut and instantly reopening the
// moment the mouse so much as twitches.
//
// Event-driven, not polled: MouseEntered/MouseExited (real Godot signals) track which registered
// control is hovered, ItemDragController.Changed (already fires on every pickup/place/swap/sell/
// buy/craft) catches a slot's contents changing while the mouse never left it, and Alt press/
// release (real discrete key events) catches the advanced-view toggle - each of the three things
// that can make the shown content stale already has a genuine event backing it, so there's no
// need to re-derive all of it from scratch every frame. The one exception is the panel's own
// on-screen position, which does need to track the mouse continuously, the same way
// ItemDragController's drag-cursor icon already does.
public partial class ItemTooltip : CanvasLayer
{
    private const float CursorOffsetX = 18f;
    private const float CursorOffsetY = 18f;

    public static ItemTooltip Instance { get; private set; }

    private readonly Dictionary<Control, Func<Item>> _sources = new();

    private PanelContainer _panel;
    private Label _label;
    private Control _hoveredControl;

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

        _label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _panel.AddChild(_label);
        AddChild(_panel);

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

        control.MouseEntered += () => OnMouseEntered(control);
        control.MouseExited += () => OnMouseExited(control);
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

    private void OnMouseEntered(Control control)
    {
        _hoveredControl = control;
        RefreshContent();
    }

    // Guarded by "is this still the hovered control" in case an out-of-order Exited (for a
    // control the mouse already left) arrives after a newer Entered - shouldn't normally happen,
    // but costs nothing to guard against clobbering a genuinely-still-hovered control.
    private void OnMouseExited(Control control)
    {
        if (_hoveredControl != control)
        {
            return;
        }

        _hoveredControl = null;
        RefreshContent();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_hoveredControl == null)
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
        if (_panel.Visible)
        {
            _panel.Position = GetViewport().GetMousePosition() + new Vector2(CursorOffsetX, CursorOffsetY);
        }
    }

    private void RefreshContent()
    {
        Item item = ResolveItem();

        if (item == null)
        {
            _panel.Visible = false;
            return;
        }

        _label.Text = Input.IsActionPressed("advanced_tooltip") ? item.BuildAdvancedTooltip() : item.BuildTooltip();
        _panel.Visible = true;
    }

    // Suppressed while something's actively held. IsInstanceValid guards against _hoveredControl
    // referencing a control freed without ever firing MouseExited - e.g. leaving the hub scene
    // entirely while a slot happened to be hovered.
    private Item ResolveItem()
    {
        if (_hoveredControl == null || !IsInstanceValid(_hoveredControl) || ItemDragController.Instance.Held != null)
        {
            return null;
        }

        return _sources.TryGetValue(_hoveredControl, out Func<Item> getItem) ? getItem() : null;
    }
}
