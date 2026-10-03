using Godot;
using System;
using System.Collections.Generic;

// A ring of wedges picked by direction: the radial_* stick actions or the mouse point, and ui_accept
// or a left click commits. Slot 0 is at the top and slots run clockwise, each centered on its own
// evenly spaced direction; a slot's weight only moves the boundaries with its neighbors. Positions
// are fixed and an empty slot stays a gap, since a player learns an item by its direction. Knows
// nothing about what its items mean - whatever hosts it reacts to Committed and handles backing out.
//
// Past SpiralWedges items it becomes a spiral: still that many wedges, with item n always at wedge
// n mod SpiralWedges. Turning the stick past the end of a lap moves onto the next one, the wedges
// swapping what they show rather than rotating, with the seam between laps opposite the stick. It
// stops at either end rather than wrapping, and the scroll wheel jumps a whole lap. Highlighted,
// HighlightChanged and Committed are item indexes, which are only wedges on a ring that fits.
//
// Reads mouse motion in _Input without consuming it, so InputDevice still sees the mouse. Anything
// full-screen the host draws around it should use MouseFilter.Ignore, or a click can be taken by the
// GUI before it reaches here.
public partial class RadialWheel : Control
{
    [Signal] public delegate void HighlightChangedEventHandler(int slot);
    [Signal] public delegate void CommittedEventHandler(int slot);

    // A ring with more items than this becomes a spiral of this many wedges.
    public const int SpiralWedges = 8;

    // How far the stick, or the mouse's virtual stick, must point before it picks a wedge. Anything
    // less drops the highlight - see ClearsHighlightAtCenter.
    private const float SelectThreshold = 0.5f;

    // Mouse travel in pixels that counts as a fully deflected virtual stick.
    private const float VirtualStickRadius = 80f;

    // How many wedges behind the highlight a spiral shows passed items; the rest show upcoming ones.
    private const int SpiralWedgesBehind = SpiralWedges / 2 - 1;

    private const float HighlightGrowFraction = 0.06f;
    private const float WedgeGapRadians = 0.02f;
    private const int ArcSegmentsPerWedge = 16;
    private const float PointerDotRadius = 6f;
    private const int LabelOutlineSize = 6;
    private const float OutlineWidth = 5f;
    private const float OutlineGap = 6f;
    private const int OutlineSegments = 96;

    // Where the lap counter sits in the hole, as a fraction of the inner radius below center.
    private const float LapCounterDrop = 0.75f;

    // A spiral's wedges fade with their distance along it from the highlight, down to this.
    private const float SpiralFarAlpha = 0.45f;
    private const float SwapFadeSeconds = 0.12f;
    private const float SeamWidth = 4f;
    private const float SeamGlowWidth = 12f;

    private static readonly Color _emptySlotColor = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color _labelColor = Colors.White;
    private static readonly Color _labelOutlineColor = Colors.Black;
    private static readonly Color _seamColor = new Color(1f, 0.95f, 0.8f);
    private static readonly Color _seamGlowColor = new Color(1f, 0.9f, 0.6f, 0.3f);

    private readonly List<RadialWheelItem> _items = new();
    private int _highlighted = -1;

    // What a spiral's laps are laid out around: the highlight, or the last one before it cleared, so
    // drifting back to the center doesn't jump the spiral back to its first lap.
    private int _anchor;

    private Vector2 _virtualStick;
    private Vector2 _pointer;
    private Color? _outlineColor;

    // Whether the stick has stayed deflected since it last picked something. While it has, turning
    // it steps along a spiral, stopping at the ends; a fresh flick lands on whatever a wedge shows.
    private bool _turning;

    // What each wedge of a spiral shows, and what it showed before its last swap, fading from one to
    // the other.
    private readonly int[] _shownItems = new int[SpiralWedges];
    private readonly int[] _swappedFromItems = new int[SpiralWedges];
    private readonly float[] _swapProgress = new float[SpiralWedges];

    // How much of the wheel's radius is the empty center, where the host shows its info.
    [Export(PropertyHint.Range, "0.1,0.9")] public float InnerRadiusFraction { get; set; } = 0.55f;

    // Off for a host that draws its own controls for each slot and only wants the pointing.
    [Export] public bool DrawWedges { get; set; } = true;

    // Off for a host that reads presses itself, like one that tells a tap from a hold.
    [Export] public bool HandlesCommit { get; set; } = true;

    // Set by the host: only one wheel reads input at a time, and not while a dialog sits over it.
    public bool AcceptsInput { get; set; }

    // Points with the look stick rather than the radial_* one, for a wheel open while the player is
    // still moving.
    public bool PointsWithLookStick { get; set; }

    // On a controller too, for a wheel where pointing nowhere is itself a choice. With the mouse, every
    // wheel drops its highlight back at the center; a controller keeps it, so letting go of the stick
    // doesn't lose it.
    public bool ClearsHighlightAtCenter { get; set; }

    public int SlotCount => _items.Count;
    public int Highlighted => _highlighted;

    // Where the stick or mouse points, up to length 1 - for a host drawing its own indicator.
    public Vector2 Pointer => _pointer;

    // A ring just outside the wedges, clear of a highlighted wedge's growth, when set - for a host
    // marking the wheel as being in some mode.
    public Color? OutlineColor
    {
        get => _outlineColor;
        set
        {
            _outlineColor = value;
            QueueRedraw();
        }
    }

    private bool IsSpiral => _items.Count > SpiralWedges;
    private int WedgeCount => IsSpiral ? SpiralWedges : _items.Count;
    private float SlotSpacing => Mathf.Tau / WedgeCount;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    // items[i] is slot i; null is an empty slot.
    public void SetItems(IReadOnlyList<RadialWheelItem> items, int highlighted = -1)
    {
        _items.Clear();
        _items.AddRange(items);
        _highlighted = highlighted < _items.Count ? highlighted : -1;
        _anchor = Math.Max(_highlighted, 0);

        // A new ring starts from neutral, not from wherever the mouse pointed into the last one.
        _virtualStick = Vector2.Zero;
        _pointer = Vector2.Zero;
        _turning = false;
        UpdateShownItems(fade: false);
        QueueRedraw();
    }

    // Swaps in new items for the same slots without resetting where the stick or mouse points - for
    // an item that changes mid-gesture.
    public void ReplaceItems(IReadOnlyList<RadialWheelItem> items)
    {
        _items.Clear();
        _items.AddRange(items);

        if (_highlighted >= _items.Count)
        {
            _highlighted = -1;
        }

        if (_anchor >= _items.Count)
        {
            _anchor = 0;
        }

        UpdateShownItems(fade: false);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (IsReadingInput() && InputDevice.UsingController)
        {
            Point(PointsWithLookStick
                ? Input.GetVector("look_left", "look_right", "look_up", "look_down")
                : Input.GetVector("radial_left", "radial_right", "radial_up", "radial_down"));
        }

        for (int wedge = 0; wedge < SpiralWedges; wedge++)
        {
            if (_swapProgress[wedge] < 1f)
            {
                _swapProgress[wedge] = Mathf.Min(_swapProgress[wedge] + (float)delta / SwapFadeSeconds, 1f);
                QueueRedraw();
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (IsReadingInput() && @event is InputEventMouseMotion motion)
        {
            _virtualStick = (_virtualStick + motion.Relative).LimitLength(VirtualStickRadius);
            Point(_virtualStick / VirtualStickRadius);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsReadingInput())
        {
            return;
        }

        if (IsSpiral && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } scroll)
        {
            int lap = scroll.ButtonIndex == MouseButton.WheelDown ? SpiralWedges : -SpiralWedges;
            SetHighlighted(Mathf.Clamp(_anchor + lap, 0, _items.Count - 1));
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!HandlesCommit)
        {
            return;
        }

        bool commitPressed = @event.IsActionPressed("ui_accept")
            || @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true };

        if (commitPressed)
        {
            TryCommit();
            GetViewport().SetInputAsHandled();
        }
    }

    private bool IsReadingInput() => AcceptsInput && IsVisibleInTree() && _items.Count > 0;

    private void Point(Vector2 direction)
    {
        _pointer = direction.LimitLength(1f);
        QueueRedraw();

        if (direction.Length() < SelectThreshold)
        {
            // Only on the way back in, so a highlight the host opened with survives until the
            // player points somewhere.
            bool returning = _turning;
            _turning = false;

            if (returning && (ClearsHighlightAtCenter || !InputDevice.UsingController))
            {
                SetHighlighted(-1);
            }

            return;
        }

        int item = ItemAt(WedgeAt(direction), nearestOnly: _turning);
        _turning = true;

        if (item >= 0)
        {
            SetHighlighted(item);
        }
    }

    private void SetHighlighted(int item)
    {
        if (item == _highlighted)
        {
            return;
        }

        _highlighted = item;

        if (item >= 0)
        {
            _anchor = item;
        }

        UpdateShownItems(fade: true);
        QueueRedraw();
        EmitSignal(SignalName.HighlightChanged, item);
    }

    // A wedge whose item changes fades from the old one to the new; a new set of items snaps.
    private void UpdateShownItems(bool fade)
    {
        if (!IsSpiral)
        {
            return;
        }

        for (int wedge = 0; wedge < SpiralWedges; wedge++)
        {
            int item = ItemAt(wedge);

            if (!fade)
            {
                _swapProgress[wedge] = 1f;
            }
            else if (item != _shownItems[wedge])
            {
                _swappedFromItems[wedge] = _shownItems[wedge];
                _swapProgress[wedge] = 0f;
            }

            _shownItems[wedge] = item;
        }
    }

    // The item a wedge shows. On a spiral that's the one belonging there nearest the anchor, so
    // the seam between laps sits opposite it. Near either end of the list that item may not exist,
    // and the wedge shows the lap on the other side of the seam instead - or nothing, with
    // nearestOnly, which is what keeps turning past an end from jumping back a lap.
    private int ItemAt(int wedge, bool nearestOnly = false)
    {
        if (!IsSpiral)
        {
            return wedge;
        }

        int item = _anchor + Mathf.PosMod(wedge - _anchor + SpiralWedgesBehind, SpiralWedges) - SpiralWedgesBehind;

        if (item >= 0 && item < _items.Count)
        {
            return item;
        }

        if (nearestOnly)
        {
            return -1;
        }

        return item < 0 ? item + SpiralWedges : item - SpiralWedges;
    }

    // Full at the anchor, fading to SpiralFarAlpha half a ring away.
    private float SpiralAlpha(int item)
    {
        if (!IsSpiral)
        {
            return 1f;
        }

        int halfRing = SpiralWedges / 2;
        return Mathf.Lerp(1f, SpiralFarAlpha, Mathf.Min(Math.Abs(item - _anchor), halfRing) / (float)halfRing);
    }

    private float WeightOf(int wedge) => IsSpiral ? 1f : _items[wedge]?.Weight ?? 1f;

    // Measured clockwise from straight up. The boundary between two neighbors sits between their
    // directions in proportion to their weights.
    private float BoundaryAfter(int wedge)
    {
        float here = WeightOf(wedge);
        float next = WeightOf((wedge + 1) % WedgeCount);
        return (wedge + here / (here + next)) * SlotSpacing;
    }

    private float BoundaryBefore(int wedge) =>
        wedge == 0 ? BoundaryAfter(WedgeCount - 1) - Mathf.Tau : BoundaryAfter(wedge - 1);

    // Where a wedge starts and ends, in screen angles (0 is +X, running clockwise) - for a host
    // drawing its own wedges.
    public (float From, float To) WedgeAngles(int wedge) =>
        (-Mathf.Pi / 2f + BoundaryBefore(wedge), -Mathf.Pi / 2f + BoundaryAfter(wedge));

    private int WedgeAt(Vector2 direction)
    {
        float fromTop = Mathf.Atan2(direction.X, -direction.Y);

        for (int wedge = 0; wedge < WedgeCount; wedge++)
        {
            float start = BoundaryBefore(wedge);

            if (Mathf.PosMod(fromTop - start, Mathf.Tau) < BoundaryAfter(wedge) - start)
            {
                return wedge;
            }
        }

        return 0;
    }

    private void TryCommit()
    {
        if (_highlighted >= 0 && _items[_highlighted] is { Enabled: true })
        {
            EmitSignal(SignalName.Committed, _highlighted);
        }
    }

    public override void _Draw()
    {
        if (_items.Count == 0 || !DrawWedges)
        {
            return;
        }

        Vector2 center = Size / 2f;
        float outerRadius = Mathf.Min(Size.X, Size.Y) / 2f;
        float innerRadius = outerRadius * InnerRadiusFraction;
        int highlightedWedge = _highlighted < 0 ? -1 : _highlighted % WedgeCount;

        for (int wedge = 0; wedge < WedgeCount; wedge++)
        {
            int shown = ItemAt(wedge);
            RadialWheelItem item = _items[shown];
            bool highlighted = wedge == highlightedWedge;
            float wedgeOuterRadius = highlighted ? outerRadius * (1f + HighlightGrowFraction) : outerRadius;
            float alpha = SpiralAlpha(shown);
            float swap = IsSpiral ? _swapProgress[wedge] : 1f;
            RadialWheelItem swappedFrom = swap < 1f ? _items[_swappedFromItems[wedge]] : null;

            Color color = WedgeColor(item, highlighted);

            if (swap < 1f)
            {
                color = WedgeColor(swappedFrom, highlighted).Lerp(color, swap);
            }

            color.A *= alpha;

            // Screen angles: 0 is +X and angles run clockwise, so straight up is -Pi/2.
            float middle = -Mathf.Pi / 2f + wedge * SlotSpacing;
            (float from, float to) = WedgeAngles(wedge);

            DrawColoredPolygon(BuildWedge(center, innerRadius, wedgeOuterRadius, from + WedgeGapRadians, to - WedgeGapRadians), color);

            Vector2 labelCenter = center + Vector2.FromAngle(middle) * (innerRadius + wedgeOuterRadius) / 2f;
            float labelWidth = (wedgeOuterRadius - innerRadius) * 1.2f;

            if (swappedFrom != null)
            {
                DrawLabel(swappedFrom.Label, labelCenter, labelWidth, alpha * (1f - swap));
            }

            if (item != null)
            {
                DrawLabel(item.Label, labelCenter, labelWidth, alpha * swap);
            }
        }

        DrawCircle(center + _pointer * innerRadius, PointerDotRadius, _labelColor);

        if (IsSpiral)
        {
            DrawSeams(center, innerRadius, outerRadius * (1f + HighlightGrowFraction));

            int laps = (_items.Count + SpiralWedges - 1) / SpiralWedges;
            int lap = _anchor / SpiralWedges + 1;
            DrawLabel($"{lap} / {laps}", center + Vector2.Down * innerRadius * LapCounterDrop, innerRadius);
        }

        if (_outlineColor.HasValue)
        {
            float outlineRadius = outerRadius * (1f + HighlightGrowFraction) + OutlineGap;
            DrawArc(center, outlineRadius, 0f, Mathf.Tau, OutlineSegments, _outlineColor.Value, OutlineWidth);
        }
    }

    // A glowing line wherever neighboring wedges aren't consecutive items: between laps, and after
    // the last item.
    private void DrawSeams(Vector2 center, float innerRadius, float outerRadius)
    {
        for (int wedge = 0; wedge < SpiralWedges; wedge++)
        {
            if (ItemAt((wedge + 1) % SpiralWedges) == ItemAt(wedge) + 1)
            {
                continue;
            }

            Vector2 direction = Vector2.FromAngle(WedgeAngles(wedge).To);
            Vector2 from = center + direction * innerRadius;
            Vector2 to = center + direction * outerRadius;
            DrawLine(from, to, _seamGlowColor, SeamGlowWidth, true);
            DrawLine(from, to, _seamColor, SeamWidth, true);
        }
    }

    private static Color WedgeColor(RadialWheelItem item, bool highlighted)
    {
        if (item == null)
        {
            return _emptySlotColor;
        }

        Color color = item.Enabled ? item.Color : item.Color.Darkened(0.6f);
        return highlighted ? color.Lightened(0.3f) : color;
    }

    public static Vector2[] BuildWedge(Vector2 center, float innerRadius, float outerRadius, float from, float to)
    {
        Vector2[] points = new Vector2[(ArcSegmentsPerWedge + 1) * 2];

        for (int i = 0; i <= ArcSegmentsPerWedge; i++)
        {
            float outerAngle = Mathf.Lerp(from, to, (float)i / ArcSegmentsPerWedge);
            float innerAngle = Mathf.Lerp(to, from, (float)i / ArcSegmentsPerWedge);
            points[i] = center + Vector2.FromAngle(outerAngle) * outerRadius;
            points[ArcSegmentsPerWedge + 1 + i] = center + Vector2.FromAngle(innerAngle) * innerRadius;
        }

        return points;
    }

    private void DrawLabel(string text, Vector2 labelCenter, float width, float alpha = 1f)
    {
        Font font = GetThemeDefaultFont();
        int fontSize = GetThemeDefaultFontSize();
        float baselineOffset = (font.GetAscent(fontSize) - font.GetDescent(fontSize)) / 2f;
        Vector2 position = new Vector2(labelCenter.X - width / 2f, labelCenter.Y + baselineOffset);

        // Wedge colors run from dark to near-white, so the outline is what keeps the text readable.
        DrawStringOutline(font, position, text, HorizontalAlignment.Center, width, fontSize, LabelOutlineSize, _labelOutlineColor with { A = alpha });
        DrawString(font, position, text, HorizontalAlignment.Center, width, fontSize, _labelColor with { A = alpha });
    }
}
