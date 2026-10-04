using Godot;
using System.Collections.Generic;
using System.Linq;

// The row of control hints along the bottom of whichever panel is on top, inside its bottom edge -
// a panel whose content reaches the bottom leaves ReservedHeight free for it. Rebuilt only when the
// top panel's hints or the device change; repositioned every frame, since panels move (the
// inventory drops below the wave bar while it shows). Lives in the persistent main scene, above
// every menu layer, so the main menu has hints too.
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
    private bool _shownForController;
    private int _shownForJoypad;

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
        List<Hint> hints = top is IHintSource source && top.IsVisibleInTree() ? source.GetHints().ToList() : null;

        if (hints == null || hints.Count == 0)
        {
            _background.Visible = false;
            _shownFor = null;
            return;
        }

        Rect2 area = top.GetGlobalRect();

        if (top != _shownFor || InputDevice.UsingController != _shownForController
            || InputDevice.LastJoypad != _shownForJoypad || !hints.SequenceEqual(_shown))
        {
            Rebuild(hints, area.Size.X - EdgeGap * 2f - SidePadding * 2f);
            _shownFor = top;
            _shown = hints;
            _shownForController = InputDevice.UsingController;
            _shownForJoypad = InputDevice.LastJoypad;
        }

        _background.Visible = true;
        _background.ResetSize();
        _background.Position = new Vector2(
            area.Position.X + (area.Size.X - _background.Size.X) / 2f,
            area.End.Y - EdgeGap - _background.Size.Y);
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
