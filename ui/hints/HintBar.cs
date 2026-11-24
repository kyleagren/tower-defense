using Godot;
using System.Collections.Generic;
using System.Linq;

// The row of control hints along the bottom of the open panel the player is pointing into (the top
// panel when they're pointing at nothing; the screen when no panel is open), inside its bottom edge - a panel whose content reaches the
// bottom leaves ReservedHeight free for it. The hints themselves always come from the top panel.
// Rebuilt only when the hints, the panel it sits under or the device change; repositioned every
// frame, since panels move (the inventory drops below the wave bar while it shows). Lives in the
// persistent main scene, above every menu layer, so the main menu has hints too.
public partial class HintBar : CanvasLayer
{
    private const float EdgeGap = 12f;
    private const float VerticalPadding = 4f;
    private const float SidePadding = 16f;
    private const int EntrySeparation = 28;
    private const int PartSeparation = 8;
    private const int LabelFontSize = 20;
    private const int HoldFontSize = 14;

    // What a panel leaves clear at its bottom: the bar plus the gap below it.
    public const float ReservedHeight = HintChip.Height + VerticalPadding * 2f + EdgeGap;

    private static readonly Color _backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color _holdColor = new Color(1f, 0.85f, 0.4f);

    private PanelContainer _background;
    private HBoxContainer _row;

    private Control _shownFor;
    private List<Hint> _shown = new();
    private List<Hint> _pending = new();
    private bool _shownForController;
    private int _shownForJoypad;

    // The last open panel the pointer was in - where the bar stays while it points at nothing.
    private Control _lastPointedPanel;

    public override void _Ready()
    {
        _background = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        StyleBoxFlat style = HudCircle.BuildCircularStyle(_backgroundColor);
        style.ContentMarginLeft = SidePadding;
        style.ContentMarginRight = SidePadding;
        style.ContentMarginTop = VerticalPadding;
        style.ContentMarginBottom = VerticalPadding;
        _background.AddThemeStyleboxOverride("panel", style);
        AddChild(_background);

        _row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _row.AddThemeConstantOverride("separation", EntrySeparation);
        _background.AddChild(_row);
    }

    public override void _Process(double delta)
    {
        Control top = UiPanelStack.Top;
        _pending.Clear();

        if (top == null)
        {
            AddFieldHints(_pending);
        }
        else if (top is IHintSource source && top.IsVisibleInTree())
        {
            source.AddHints(_pending);
        }

        if (_pending.Count == 0)
        {
            _background.Visible = false;
            _shownFor = null;
            _shown.Clear();
            return;
        }

        Control anchor = top == null ? null : ResolveAnchor(top);
        Rect2 area = anchor?.GetGlobalRect() ?? GetViewport().GetVisibleRect();

        if (anchor != _shownFor || InputDevice.UsingController != _shownForController
            || InputDevice.LastJoypad != _shownForJoypad || !_pending.SequenceEqual(_shown))
        {
            Rebuild(_pending, area.Size.X - EdgeGap * 2f - SidePadding * 2f);
            (_shown, _pending) = (_pending, _shown);
            _shownFor = anchor;
            _shownForController = InputDevice.UsingController;
            _shownForJoypad = InputDevice.LastJoypad;
        }

        _background.Visible = true;
        _background.ResetSize();
        _background.Position = new Vector2(
            area.Position.X + (area.Size.X - _background.Size.X) / 2f,
            area.End.Y - EdgeGap - _background.Size.Y);
    }

    // With no panel open, the local character's own components speak - placing a Ritual, picking a
    // token. They're direct children of its body.
    private static void AddFieldHints(List<Hint> hints)
    {
        PlayerCharacter character = LevelContext.Current?.LocalActiveCharacter;

        if (character == null)
        {
            return;
        }

        foreach (Node child in character.GetChildren())
        {
            if (child is IFieldHintSource source)
            {
                source.AddHints(hints);
            }
        }
    }

    // The focused control on a controller, the hovered one on a mouse.
    public static Control PointedControl(Viewport viewport)
    {
        return InputDevice.UsingController ? viewport.GuiGetFocusOwner() : viewport.GuiGetHoveredControl();
    }

    // The panel the pointer is in; else the last one it was in, if that's still open; else the top one.
    private Control ResolveAnchor(Control top)
    {
        Control pointedPanel = PanelHolding(PointedControl(GetViewport()));

        if (pointedPanel != null)
        {
            _lastPointedPanel = pointedPanel;
            return pointedPanel;
        }

        if (_lastPointedPanel != null && IsInstanceValid(_lastPointedPanel)
            && UiPanelStack.Open.Contains(_lastPointedPanel) && _lastPointedPanel.IsVisibleInTree())
        {
            return _lastPointedPanel;
        }

        _lastPointedPanel = null;
        return top;
    }

    // The topmost open panel holding the pointer - a station and Inventory sit side by side, and the
    // bar goes under whichever one the player is working in.
    private static Control PanelHolding(Control pointed)
    {
        if (pointed == null)
        {
            return null;
        }

        for (int i = UiPanelStack.Open.Count - 1; i >= 0; i--)
        {
            Control panel = UiPanelStack.Open[i];

            if (panel.IsVisibleInTree() && (panel == pointed || panel.IsAncestorOf(pointed)))
            {
                return panel;
            }
        }

        return null;
    }

    // Adds hints in order until the next one would overflow the panel's width.
    private void Rebuild(List<Hint> hints, float maxRowWidth)
    {
        foreach (Node child in _row.GetChildren())
        {
            _row.RemoveChild(child);
            child.QueueFree();
        }

        foreach (Hint hint in hints)
        {
            Control entry = CreateEntry(hint);
            _row.AddChild(entry);

            if (_row.GetChildCount() > 1 && _row.GetCombinedMinimumSize().X > maxRowWidth)
            {
                _row.RemoveChild(entry);
                entry.QueueFree();
                break;
            }
        }
    }

    private static Control CreateEntry(Hint hint)
    {
        HBoxContainer entry = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        entry.AddThemeConstantOverride("separation", PartSeparation);

        if (hint.Hold)
        {
            entry.AddChild(CreateLabel("HOLD", HoldFontSize, _holdColor));
        }

        entry.AddChild(HintChip.Create(hint));
        entry.AddChild(CreateLabel(hint.Label, LabelFontSize, Colors.White));
        return entry;
    }

    private static Label CreateLabel(string text, int fontSize, Color color)
    {
        Label label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
