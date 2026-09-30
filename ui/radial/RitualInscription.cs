using Godot;
using System.Collections.Generic;

// The build menu's Ritual Inscription, where runes are inscribed onto a spell to make it a complete
// ritual. Picked by direction exactly like a RadialWheel - it wraps one with its wedges hidden - but
// drawn as ordinary controls: sockets as circles on the diagonals and actions as buttons on the
// cardinals, each lit up while it's pointed at.
//
// Reads presses itself rather than leaving them to the wheel, so a slot can tell a tap from a hold:
// a slot with no hold time fires Tapped on press; one with a hold time fires Tapped on release, or
// Held once pressed that long. Pointing away mid-hold cancels it.
public partial class RitualInscription : Control
{
    [Signal] public delegate void HighlightChangedEventHandler(int slot);
    [Signal] public delegate void TappedEventHandler(int slot);
    [Signal] public delegate void HeldEventHandler(int slot);

    // A socket takes twice the angle an action does: sockets are picked most, and the diagonals they
    // sit on are the hardest directions to land.
    public const float SocketWeight = 2f;
    public const float SocketDiameter = 182f;

    private const int SlotCount = 8;
    private const float SlotDistance = 325f;
    private const float BackdropRadius = 533f;
    private const int HighlightBorderWidth = 4;
    private const int LabelOutlineSize = 6;
    private const float PointerTravel = 143f;
    private const float PointerDotRadius = 18f;

    private const int SlotFontSize = 20;
    private const int TitleFontSize = 28;
    private const int CostFontSize = 20;
    private const int DescriptionFontSize = 18;

    private static readonly Vector2 _actionSize = new Vector2(247f, 83f);
    private static readonly Vector2 _centerInfoSize = new Vector2(338f, 182f);
    private static readonly Color _highlightBorderColor = Colors.White;
    private static readonly Color _backdropColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color _holdFillColor = new Color(1f, 1f, 1f, 0.35f);
    private static readonly Color _pointerColor = new Color(1f, 1f, 1f, 0.3f);
    private static readonly Color _dimmedModulate = new Color(1f, 1f, 1f, 0.35f);

    private RadialWheel _wheel;
    private readonly Panel[] _slotPanels = new Panel[SlotCount];
    private readonly Label[] _slotLabels = new Label[SlotCount];
    private readonly ColorRect[] _holdFills = new ColorRect[SlotCount];
    private readonly List<RadialWheelItem> _items = new();
    private readonly float[] _holdSeconds = new float[SlotCount];

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

    // Sockets sit on the diagonals, actions on the cardinals.
    public static bool IsSocketSlot(int slot) => slot % 2 == 1;

    // Where a slot sits relative to this control's center.
    public static Vector2 SlotOffset(int slot) =>
        Vector2.FromAngle(-Mathf.Pi / 2f + slot * Mathf.Tau / SlotCount) * SlotDistance;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _wheel = new RadialWheel { DrawWedges = false, HandlesCommit = false };
        _wheel.SetAnchorsPreset(LayoutPreset.FullRect);
        _wheel.HighlightChanged += OnWheelHighlightChanged;
        AddChild(_wheel);

        for (int slot = 0; slot < SlotCount; slot++)
        {
            BuildSlot(slot);
        }

        BuildCenterInfo();
    }

    // items[i] and holdSeconds[i] are slot i, clockwise from the top. A hold time of 0 fires on press.
    public void SetItems(IReadOnlyList<RadialWheelItem> items, IReadOnlyList<float> holdSeconds, int highlighted = -1)
    {
        StoreItems(items, holdSeconds);
        _wheel.SetItems(items, highlighted);
        Refresh();
    }

    public void ReplaceItems(IReadOnlyList<RadialWheelItem> items, IReadOnlyList<float> holdSeconds)
    {
        StoreItems(items, holdSeconds);
        _wheel.ReplaceItems(items);
        Refresh();
    }

    // While a ring is open around one socket, everything else fades so that socket and its ring
    // stand out. -1 restores everything.
    public void SetFocusSocket(int slot)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            _slotPanels[i].Modulate = slot < 0 || i == slot ? Colors.White : _dimmedModulate;
        }
    }

    // Shows a candidate in a socket without committing it; the next SetItems or ReplaceItems
    // restores the real label.
    public void PreviewSocketLabel(int slot, string text)
    {
        _slotLabels[slot].Text = text;
    }

    private void StoreItems(IReadOnlyList<RadialWheelItem> items, IReadOnlyList<float> holdSeconds)
    {
        _items.Clear();
        _items.AddRange(items);

        for (int slot = 0; slot < SlotCount; slot++)
        {
            _holdSeconds[slot] = slot < holdSeconds.Count ? holdSeconds[slot] : 0f;
        }

        CancelHold();
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

        if (slot < 0 || slot >= _items.Count || _items[slot] is not { Enabled: true })
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
        float progress = _heldFor / _holdSeconds[_pressedSlot];

        if (progress >= 1f)
        {
            int slot = _pressedSlot;
            CancelHold();
            EmitSignal(SignalName.Held, slot);
            return;
        }

        SetHoldFill(_pressedSlot, progress);
    }

    // Drawn here rather than as children so both sit behind every slot and label: the backdrop
    // behind everything, and the pointer dot behind the center text it passes under.
    public override void _Draw()
    {
        Vector2 center = Size / 2f;
        DrawCircle(center, BackdropRadius, _backdropColor);

        if (AcceptsInput)
        {
            DrawCircle(center + _wheel.Pointer * PointerTravel, PointerDotRadius, _pointerColor);
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

    // Only action buttons show a fill; on a socket circle a filling rectangle reads as a glitch, and
    // a socket's hold is short enough not to need one.
    private void SetHoldFill(int slot, float progress)
    {
        if (!IsSocketSlot(slot))
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
        Vector2 size = IsSocketSlot(slot) ? new Vector2(SocketDiameter, SocketDiameter) : _actionSize;

        Panel panel = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        PlaceAroundCenter(panel, SlotOffset(slot), size);
        AddChild(panel);

        ColorRect holdFill = new ColorRect { Color = _holdFillColor, MouseFilter = MouseFilterEnum.Ignore };
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
        PlaceAroundCenter(info, Vector2.Zero, _centerInfoSize);
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
            RadialWheelItem item = slot < _items.Count ? _items[slot] : null;
            _slotPanels[slot].Visible = item != null;

            if (item == null)
            {
                continue;
            }

            bool highlighted = slot == _wheel.Highlighted;
            Color fill = item.Enabled ? item.Color : item.Color.Darkened(0.6f);
            StyleBoxFlat style = new StyleBoxFlat { BgColor = highlighted ? fill.Lightened(0.2f) : fill };
            style.SetCornerRadiusAll(IsSocketSlot(slot) ? 999 : 8);

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
