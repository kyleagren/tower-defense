using Godot;
using System.Collections.Generic;

// A wheel drawn as buttons rather than wedges. Picked by direction exactly like a RadialWheel - it
// wraps one with its wedges hidden - with each button lit up while it's pointed at. A subclass
// supplies the layout: how many slots, where each sits, its size and shape, and its share of the
// pointing angle.
//
// Reads presses itself rather than leaving them to the wheel, so a slot can tell a tap from a hold:
// a slot with no hold time fires Tapped on press; one with a hold time fires Tapped on release, or
// Held once pressed that long. Pointing away mid-hold cancels it.
public abstract partial class ButtonWheel : Control
{
    [Signal] public delegate void HighlightChangedEventHandler(int slot);
    [Signal] public delegate void TappedEventHandler(int slot);
    [Signal] public delegate void HeldEventHandler(int slot);

    private const int HighlightBorderWidth = 4;
    private const int LabelOutlineSize = 6;
    private const float HoldRingWidth = 6f;
    private const float HoldRingGap = 8f;
    private const int HoldRingSegments = 48;
    private const float BackdropOutlineWidth = 5f;
    private const int BackdropOutlineSegments = 128;

    private static readonly Color _highlightBorderColor = Colors.White;
    private static readonly Color _backdropColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color _holdColor = new Color(1f, 1f, 1f, 0.35f);
    private static readonly Color _holdRingColor = new Color(1f, 1f, 1f, 0.8f);
    private static readonly Color _pointerColor = new Color(1f, 1f, 1f, 0.3f);
    private static readonly Color _fadedModulate = new Color(1f, 1f, 1f, 0.35f);

    private RadialWheel _wheel;
    private Panel[] _slotPanels;
    private Label[] _slotLabels;
    private ColorRect[] _holdFills;
    private float[] _holdSeconds;

    // What the host set, with null for a slot that shows nothing.
    private readonly List<RadialWheelItem> _items = new();

    private int _pressedSlot = -1;
    private float _heldFor;

    public Label TitleLabel { get; private set; }
    public Label CostLabel { get; private set; }
    public Label DescriptionLabel { get; private set; }

    public bool AcceptsInput
    {
        get => _wheel.AcceptsInput;
        set => _wheel.AcceptsInput = value;
    }

    // A ring along the backdrop circle's edge when set - for a host marking the wheel as being in
    // some mode.
    public Color? BackdropOutlineColor { get; set; }

    protected abstract int SlotCount { get; }
    protected abstract Vector2 OffsetOf(int slot);
    protected abstract Vector2 SizeOf(int slot);
    protected virtual bool IsRound(int slot) => false;

    // This slot's share of the pointing angle - see RadialWheelItem.Weight. A weight of 0 makes a
    // slot unreachable, for a layout that leaves a direction unused.
    protected virtual float WeightOf(int slot) => 1f;

    // 0 draws no backdrop.
    protected virtual float BackdropRadius => 0f;
    protected virtual float PointerTravel => 110f;
    protected virtual float PointerDotRadius => 14f;
    protected virtual Vector2 CenterInfoSize => new Vector2(260f, 140f);
    protected virtual int SlotFontSize => 20;
    protected virtual int TitleFontSize => 28;
    protected virtual int CostFontSize => 20;
    protected virtual int DescriptionFontSize => 18;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _wheel = new RadialWheel { DrawWedges = false, HandlesCommit = false };
        _wheel.SetAnchorsPreset(LayoutPreset.FullRect);
        _wheel.HighlightChanged += OnWheelHighlightChanged;
        AddChild(_wheel);

        _slotPanels = new Panel[SlotCount];
        _slotLabels = new Label[SlotCount];
        _holdFills = new ColorRect[SlotCount];
        _holdSeconds = new float[SlotCount];

        for (int slot = 0; slot < SlotCount; slot++)
        {
            BuildSlot(slot);
        }

        BuildCenterInfo();
    }

    // items[i] and holdSeconds[i] are slot i. A null item shows nothing and can't be picked; a hold
    // time of 0 fires on press.
    public void SetItems(IReadOnlyList<RadialWheelItem> items, IReadOnlyList<float> holdSeconds, int highlighted = -1)
    {
        StoreItems(items, holdSeconds);
        _wheel.SetItems(WheelItems(), highlighted);
        Refresh();
    }

    public void ReplaceItems(IReadOnlyList<RadialWheelItem> items, IReadOnlyList<float> holdSeconds)
    {
        StoreItems(items, holdSeconds);
        _wheel.ReplaceItems(WheelItems());
        Refresh();
    }

    // While something is open over one slot, everything else fades so that slot stands out. -1
    // restores everything.
    public void FadeAllExcept(int slot)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            _slotPanels[i].Modulate = slot < 0 || i == slot ? Colors.White : _fadedModulate;
        }
    }

    // Shows other text on a slot without changing its item; the next SetItems or ReplaceItems
    // restores the real label.
    public void SetSlotLabel(int slot, string text)
    {
        _slotLabels[slot].Text = text;
    }

    private void StoreItems(IReadOnlyList<RadialWheelItem> items, IReadOnlyList<float> holdSeconds)
    {
        _items.Clear();

        for (int slot = 0; slot < SlotCount; slot++)
        {
            _items.Add(slot < items.Count ? items[slot] : null);
            _holdSeconds[slot] = slot < holdSeconds.Count ? holdSeconds[slot] : 0f;
        }

        CancelHold();
    }

    // The pointing wheel gets every slot at its layout's weight. An empty slot becomes a disabled
    // item rather than null, since null would fall back to the default weight.
    private List<RadialWheelItem> WheelItems()
    {
        List<RadialWheelItem> wheelItems = new List<RadialWheelItem>(SlotCount);

        for (int slot = 0; slot < SlotCount; slot++)
        {
            RadialWheelItem item = _items[slot] ?? new RadialWheelItem { Enabled = false };
            wheelItems.Add(item with { Weight = WeightOf(slot) });
        }

        return wheelItems;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!AcceptsInput || !IsVisibleInTree())
        {
            return;
        }

        bool pressed = @event.IsActionPressed("ui_accept")
            || @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true };
        bool released = @event.IsActionReleased("ui_accept")
            || @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false };

        if (pressed)
        {
            OnPressed();
            GetViewport().SetInputAsHandled();
        }
        else if (released && _pressedSlot >= 0)
        {
            int slot = _pressedSlot;
            CancelHold();
            EmitSignal(SignalName.Tapped, slot);
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnPressed()
    {
        int slot = _wheel.Highlighted;

        if (slot < 0 || slot >= SlotCount || _items[slot] is not { Enabled: true })
        {
            return;
        }

        if (_holdSeconds[slot] <= 0f)
        {
            EmitSignal(SignalName.Tapped, slot);
            return;
        }

        _pressedSlot = slot;
        _heldFor = 0f;
    }

    public override void _Process(double delta)
    {
        QueueRedraw();

        if (_pressedSlot < 0)
        {
            return;
        }

        if (_wheel.Highlighted != _pressedSlot)
        {
            CancelHold();
            return;
        }

        _heldFor += (float)delta;

        if (_heldFor >= _holdSeconds[_pressedSlot])
        {
            int slot = _pressedSlot;
            CancelHold();
            EmitSignal(SignalName.Held, slot);
            return;
        }

        SetHoldFill(_pressedSlot, HoldProgress);
    }

    private float HoldProgress => _pressedSlot < 0 ? 0f : _heldFor / _holdSeconds[_pressedSlot];

    // Drawn here rather than as children so all of it sits behind the buttons and labels: the
    // backdrop behind everything, the pointer dot behind the center text it passes under, and a
    // round button's hold ring just outside its edge.
    public override void _Draw()
    {
        Vector2 center = Size / 2f;

        if (BackdropRadius > 0f)
        {
            DrawCircle(center, BackdropRadius, _backdropColor);

            if (BackdropOutlineColor.HasValue)
            {
                DrawArc(center, BackdropRadius, 0f, Mathf.Tau, BackdropOutlineSegments, BackdropOutlineColor.Value, BackdropOutlineWidth);
            }
        }

        if (AcceptsInput)
        {
            DrawCircle(center + _wheel.Pointer * PointerTravel, PointerDotRadius, _pointerColor);
        }

        if (_pressedSlot >= 0 && IsRound(_pressedSlot))
        {
            float radius = SizeOf(_pressedSlot).X / 2f + HoldRingGap;
            float start = -Mathf.Pi / 2f;
            DrawArc(center + OffsetOf(_pressedSlot), radius, start, start + HoldProgress * Mathf.Tau, HoldRingSegments, _holdRingColor, HoldRingWidth);
        }
    }

    private void CancelHold()
    {
        if (_pressedSlot >= 0)
        {
            SetHoldFill(_pressedSlot, 0f);
        }

        _pressedSlot = -1;
        _heldFor = 0f;
    }

    // Rectangular buttons fill from the left; round ones get the ring in _Draw instead, since a
    // filling rectangle inside a circle reads as a glitch.
    private void SetHoldFill(int slot, float progress)
    {
        if (!IsRound(slot))
        {
            _holdFills[slot].AnchorRight = progress;
        }
    }

    private void OnWheelHighlightChanged(int slot)
    {
        Refresh();
        EmitSignal(SignalName.HighlightChanged, slot);
    }

    private void BuildSlot(int slot)
    {
        Panel panel = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        PlaceAroundCenter(panel, OffsetOf(slot), SizeOf(slot));
        AddChild(panel);

        ColorRect holdFill = new ColorRect { Color = _holdColor, MouseFilter = MouseFilterEnum.Ignore };
        holdFill.SetAnchorsPreset(LayoutPreset.FullRect);
        holdFill.AnchorRight = 0f;
        panel.AddChild(holdFill);

        Label label = CreateOutlinedLabel(SlotFontSize);
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        label.VerticalAlignment = VerticalAlignment.Center;
        panel.AddChild(label);

        _slotPanels[slot] = panel;
        _holdFills[slot] = holdFill;
        _slotLabels[slot] = label;
    }

    private void BuildCenterInfo()
    {
        VBoxContainer info = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        PlaceAroundCenter(info, Vector2.Zero, CenterInfoSize);
        AddChild(info);

        TitleLabel = CreateOutlinedLabel(TitleFontSize);
        CostLabel = CreateOutlinedLabel(CostFontSize);
        DescriptionLabel = CreateOutlinedLabel(DescriptionFontSize);
        info.AddChild(TitleLabel);
        info.AddChild(CostLabel);
        info.AddChild(DescriptionLabel);
    }

    private void Refresh()
    {
        for (int slot = 0; slot < SlotCount; slot++)
        {
            RadialWheelItem item = _items[slot];
            _slotPanels[slot].Visible = item != null;

            if (item == null)
            {
                continue;
            }

            bool highlighted = slot == _wheel.Highlighted;
            Color fill = item.Enabled ? item.Color : item.Color.Darkened(0.6f);
            StyleBoxFlat style = new StyleBoxFlat { BgColor = highlighted ? fill.Lightened(0.2f) : fill };
            style.SetCornerRadiusAll(IsRound(slot) ? 999 : 8);

            if (highlighted)
            {
                style.BorderColor = _highlightBorderColor;
                style.SetBorderWidthAll(HighlightBorderWidth);
            }

            _slotPanels[slot].AddThemeStyleboxOverride("panel", style);
            _slotLabels[slot].Text = item.Label;
        }
    }

    // Anchored on this control's center and offset in pixels, so the layout stays centered at any
    // screen size.
    private static void PlaceAroundCenter(Control control, Vector2 offset, Vector2 size)
    {
        control.AnchorLeft = 0.5f;
        control.AnchorRight = 0.5f;
        control.AnchorTop = 0.5f;
        control.AnchorBottom = 0.5f;
        control.OffsetLeft = offset.X - size.X / 2f;
        control.OffsetRight = offset.X + size.X / 2f;
        control.OffsetTop = offset.Y - size.Y / 2f;
        control.OffsetBottom = offset.Y + size.Y / 2f;
    }

    private static Label CreateOutlinedLabel(int fontSize)
    {
        Label label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeConstantOverride("outline_size", LabelOutlineSize);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        return label;
    }
}
