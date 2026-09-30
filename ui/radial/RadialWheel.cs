using Godot;
using System.Collections.Generic;

// A ring of wedges picked by direction: the radial_* stick actions or the mouse point, and ui_accept
// or a left click commits. Slot 0 is at the top and slots run clockwise, each centered on its own
// evenly spaced direction; a slot's weight only moves the boundaries with its neighbors. Positions
// are fixed and an empty slot stays a gap, since a player learns an item by its direction. Knows
// nothing about what its items mean - whatever hosts it reacts to Committed and handles backing out.
//
// Reads mouse motion in _Input without consuming it, so InputDevice still sees the mouse. Anything
// full-screen the host draws around it should use MouseFilter.Ignore, or a click can be taken by the
// GUI before it reaches here.
public partial class RadialWheel : Control
{
    [Signal] public delegate void HighlightChangedEventHandler(int slot);
    [Signal] public delegate void CommittedEventHandler(int slot);

    // How far the stick, or the mouse's virtual stick, must point before it picks a wedge. Anything
    // less keeps the last highlight, so letting go of the stick doesn't lose it.
    private const float SelectThreshold = 0.5f;

    // Mouse travel in pixels that counts as a fully deflected virtual stick.
    private const float VirtualStickRadius = 80f;

    private const float HighlightGrowFraction = 0.06f;
    private const float WedgeGapRadians = 0.02f;
    private const int ArcSegmentsPerWedge = 16;
    private const float PointerDotRadius = 6f;
    private const int LabelOutlineSize = 6;

    private static readonly Color _emptySlotColor = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color _labelColor = Colors.White;
    private static readonly Color _labelOutlineColor = Colors.Black;

    private readonly List<RadialWheelItem> _items = new();
    private int _highlighted = -1;
    private Vector2 _virtualStick;
    private Vector2 _pointer;

    // How much of the wheel's radius is the empty center, where the host shows its info.
    [Export(PropertyHint.Range, "0.1,0.9")] public float InnerRadiusFraction { get; set; } = 0.55f;

    // Off for a host that draws its own controls for each slot and only wants the pointing.
    [Export] public bool DrawWedges { get; set; } = true;

    // Off for a host that reads presses itself, like one that tells a tap from a hold.
    [Export] public bool HandlesCommit { get; set; } = true;

    // Set by the host: only one wheel reads input at a time, and not while a dialog sits over it.
    public bool AcceptsInput { get; set; }

    public int SlotCount => _items.Count;
    public int Highlighted => _highlighted;

    // Where the stick or mouse points, up to length 1 - for a host drawing its own indicator.
    public Vector2 Pointer => _pointer;

    private float SlotSpacing => Mathf.Tau / _items.Count;

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

        // A new ring starts from neutral, not from wherever the mouse pointed into the last one.
        _virtualStick = Vector2.Zero;
        _pointer = Vector2.Zero;
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

        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (IsReadingInput() && InputDevice.UsingController)
        {
            Point(Input.GetVector("radial_left", "radial_right", "radial_up", "radial_down"));
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
        if (!IsReadingInput() || !HandlesCommit)
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
            return;
        }

        int slot = SlotAt(direction);

        if (slot != _highlighted)
        {
            _highlighted = slot;
            EmitSignal(SignalName.HighlightChanged, slot);
        }
    }

    private static float WeightOf(RadialWheelItem item) => item?.Weight ?? 1f;

    // Measured clockwise from straight up. The boundary between two neighbors sits between their
    // directions in proportion to their weights.
    private float BoundaryAfter(int slot)
    {
        float here = WeightOf(_items[slot]);
        float next = WeightOf(_items[(slot + 1) % _items.Count]);
        return (slot + here / (here + next)) * SlotSpacing;
    }

    private float BoundaryBefore(int slot) =>
        slot == 0 ? BoundaryAfter(_items.Count - 1) - Mathf.Tau : BoundaryAfter(slot - 1);

    private int SlotAt(Vector2 direction)
    {
        float fromTop = Mathf.Atan2(direction.X, -direction.Y);

        for (int slot = 0; slot < _items.Count; slot++)
        {
            float start = BoundaryBefore(slot);

            if (Mathf.PosMod(fromTop - start, Mathf.Tau) < BoundaryAfter(slot) - start)
            {
                return slot;
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

        for (int slot = 0; slot < _items.Count; slot++)
        {
            RadialWheelItem item = _items[slot];
            bool highlighted = slot == _highlighted;
            float slotOuterRadius = highlighted ? outerRadius * (1f + HighlightGrowFraction) : outerRadius;

            // Screen angles: 0 is +X and angles run clockwise, so straight up is -Pi/2.
            float middle = -Mathf.Pi / 2f + slot * SlotSpacing;
            float from = -Mathf.Pi / 2f + BoundaryBefore(slot) + WedgeGapRadians;
            float to = -Mathf.Pi / 2f + BoundaryAfter(slot) - WedgeGapRadians;

            DrawColoredPolygon(BuildWedge(center, innerRadius, slotOuterRadius, from, to), WedgeColor(item, highlighted));

            if (item != null)
            {
                Vector2 labelCenter = center + Vector2.FromAngle(middle) * (innerRadius + slotOuterRadius) / 2f;
                DrawLabel(item.Label, labelCenter, (slotOuterRadius - innerRadius) * 1.2f);
            }
        }

        DrawCircle(center + _pointer * innerRadius, PointerDotRadius, _labelColor);
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

    private static Vector2[] BuildWedge(Vector2 center, float innerRadius, float outerRadius, float from, float to)
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

    private void DrawLabel(string text, Vector2 labelCenter, float width)
    {
        Font font = GetThemeDefaultFont();
        int fontSize = GetThemeDefaultFontSize();
        float baselineOffset = (font.GetAscent(fontSize) - font.GetDescent(fontSize)) / 2f;
        Vector2 position = new Vector2(labelCenter.X - width / 2f, labelCenter.Y + baselineOffset);

        // Wedge colors run from dark to near-white, so the outline is what keeps the text readable.
        DrawStringOutline(font, position, text, HorizontalAlignment.Center, width, fontSize, LabelOutlineSize, _labelOutlineColor);
        DrawString(font, position, text, HorizontalAlignment.Center, width, fontSize, _labelColor);
    }
}
