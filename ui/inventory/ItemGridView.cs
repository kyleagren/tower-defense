using Godot;
using System;
using SlotHandle = ItemDragController.SlotHandle;

// An ItemGrid on screen. Every cell is its own focusable Button registered with ItemDragController, so
// clicks, drags, tooltips and controller focus all work per cell; items draw on a layer above the
// cells, each across its whole footprint. The cell under the pointer or focus is outlined - the whole
// item, when there's one there - and while an item is held, the cells it would cover are tinted by
// whether it fits, would swap, or can't go there.
public partial class ItemGridView : Control
{
    private static readonly Color UnusableColor = new Color(0.32f, 0.14f, 0.14f);
    private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.9f);
    private static readonly Color LiftedFromColor = new Color(0.85f, 0.75f, 0.15f, 0.45f);
    private static readonly Color FitsColor = new Color(0.3f, 0.9f, 0.3f, 0.4f);
    private static readonly Color SwapsColor = new Color(0.95f, 0.8f, 0.2f, 0.4f);
    private static readonly Color BlockedColor = new Color(0.9f, 0.25f, 0.25f, 0.4f);

    private static readonly (string Action, Vector2I Step, Side Side)[] _focusMoves =
    {
        ("ui_left", Vector2I.Left, Side.Left),
        ("ui_right", Vector2I.Right, Side.Right),
        ("ui_up", Vector2I.Up, Side.Top),
        ("ui_down", Vector2I.Down, Side.Bottom)
    };

    [Export] public float CellSize { get; private set; } = 60f;
    [Export] public float CellGap { get; private set; } = 2f;

    // A panel's own handling of a press, tried before the usual pick-up/place; true if it handled it.
    public Func<Vector2I, bool> TryHandlePress { get; set; }

    public event Action<Vector2I, InputEvent> CellGuiInput;

    public int Columns { get; private set; }
    public int Rows { get; private set; }

    private Func<ItemGrid> _getGrid;
    private Func<Item, bool> _isUsable;
    private Button[] _cells;
    private Control _itemLayer;
    private Panel _liftedFromMarker;
    private Panel _hoverMarker;
    private Vector2I? _hoveredCell;

    // The item last lifted out of this view and where it sat, marked until it's no longer the one held.
    private Item _liftedItem;
    private ItemGrid _liftedGrid;
    private Vector2I _liftedFrom;

    // getGrid is read on every use, so one view can show different grids (stash tabs) without
    // rebuilding. isUsable, when given, marks the items it rejects in place of their rarity color.
    public void Setup(Func<ItemGrid> getGrid, Func<Item, bool> isUsable)
    {
        _getGrid = getGrid;
        _isUsable = isUsable;

        ItemGrid grid = getGrid();
        Columns = grid.Columns;
        Rows = grid.Rows;
        CustomMinimumSize = FootprintSize(new Vector2I(Columns, Rows));
        _cells = new Button[Columns * Rows];

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                BuildCell(new Vector2I(column, row));
            }
        }

        _itemLayer = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _itemLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_itemLayer);

        _liftedFromMarker = BuildMarker(Fill(LiftedFromColor));
        _hoverMarker = BuildMarker(null);

        VisibilityChanged += () => _hoveredCell = null;
        ItemDragController.Instance.Changed += OnContentsChanged;
        EventBus.Instance.InventoryChanged += OnContentsChanged;
    }

    public override void _ExitTree()
    {
        if (_getGrid == null)
        {
            return;
        }

        ItemDragController.Instance.Changed -= OnContentsChanged;
        EventBus.Instance.InventoryChanged -= OnContentsChanged;
    }

    public Button Cell(Vector2I cell) => _cells[cell.Y * Columns + cell.X];

    private void OnContentsChanged()
    {
        if (IsVisibleInTree())
        {
            Refresh();
        }
    }

    // One Panel per item rather than per cell, so they're rebuilt rather than updated in place.
    public void Refresh()
    {
        foreach (Node child in _itemLayer.GetChildren())
        {
            _itemLayer.RemoveChild(child);
            child.QueueFree();
        }

        foreach ((Item item, Vector2I anchor) in _getGrid().Placements())
        {
            _itemLayer.AddChild(BuildItemVisual(item, anchor));
        }

        RefreshLiftedFrom();
        RefreshHover();
    }

    private void BuildCell(Vector2I cell)
    {
        SlotHandle slot = GridSlot(_getGrid, cell, item => _getGrid().AnchorCenteredOn(item, cell), () => ReturnSlot(cell));
        Button button = new Button { Position = CellPosition(cell), Size = FootprintSize(Vector2I.One) };

        button.Pressed += () => OnCellPressed(cell, slot, button);
        button.GuiInput += @event => ItemDragController.Instance.OnSlotGuiInput(slot, button, @event);
        button.GuiInput += @event => CellGuiInput?.Invoke(cell, @event);
        button.GuiInput += @event => OnCellFocusInput(cell, button, @event);
        button.MouseEntered += () => Hover(cell);
        button.MouseExited += () => Unhover(cell);
        button.FocusEntered += () => Hover(cell);
        ItemDragController.Instance.RegisterSlot(button, slot);
        ItemTooltip.Instance.Register(button, () => _getGrid().At(cell));

        AddChild(button);
        _cells[cell.Y * Columns + cell.X] = button;
    }

    private void OnCellPressed(Vector2I cell, SlotHandle slot, Button button)
    {
        if (ItemDragController.Instance.ConsumeDragJustHandled() || TryHandlePress?.Invoke(cell) == true)
        {
            return;
        }

        ItemDragController.Instance.OnSlotPressed(slot, button);
    }

    // With nothing held, a focus move leaves the whole item focus is on, rather than stepping through
    // each of its cells. Holding an item it steps a cell at a time, since where the item lands
    // follows the focused cell. Past the grid's edge, the edge cell's own focus neighbor takes over.
    private void OnCellFocusInput(Vector2I cell, Button button, InputEvent @event)
    {
        if (ItemDragController.Instance.Held != null)
        {
            return;
        }

        foreach ((string action, Vector2I step, Side side) in _focusMoves)
        {
            if (!@event.IsActionPressed(action, allowEcho: true))
            {
                continue;
            }

            ItemGrid grid = _getGrid();
            Item under = grid.At(cell);

            if (under == null)
            {
                return;
            }

            Vector2I exit = cell;

            while (IsOnGrid(exit + step) && grid.At(exit + step) == under)
            {
                exit += step;
            }

            Control next = IsOnGrid(exit + step) ? Cell(exit + step) : Cell(exit).FindValidFocusNeighbor(side);
            next?.GrabFocus();
            button.AcceptEvent();
            return;
        }
    }

    private bool IsOnGrid(Vector2I cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Columns && cell.Y < Rows;

    // A place in a grid: an item goes in at anchorFor(item), and taking out takes whatever covers cell.
    private static SlotHandle GridSlot(Func<ItemGrid> grid, Vector2I cell, Func<Item, Vector2I> anchorFor, Func<SlotHandle> returnSlot) => new SlotHandle(
        getItem: () => grid().At(cell),
        placeItem: item => item == null ? TakeOut(grid(), cell) : grid().Place(item, anchorFor(item)),
        canAccept: item => grid().CanPlace(item, anchorFor(item), out _),
        wouldDisplace: item => grid().CanPlace(item, anchorFor(item), out Item displaced) ? displaced : null,
        returnSlot: returnSlot);

    private static Item TakeOut(ItemGrid grid, Vector2I cell)
    {
        Item item = grid.At(cell);
        grid.Remove(item);
        return item;
    }

    // Called just before the item at cell is lifted: a slot that puts it back exactly where it is now,
    // in this grid even if the view has since switched to another.
    private SlotHandle ReturnSlot(Vector2I cell)
    {
        ItemGrid grid = _getGrid();
        _liftedItem = grid.At(cell);
        _liftedGrid = grid;
        _liftedFrom = grid.AnchorOf(_liftedItem).Value;

        Vector2I anchor = _liftedFrom;
        return GridSlot(() => grid, anchor, _ => anchor, returnSlot: null);
    }

    private void Hover(Vector2I cell)
    {
        _hoveredCell = cell;
        RefreshHover();
    }

    private void Unhover(Vector2I cell)
    {
        if (_hoveredCell == cell)
        {
            _hoveredCell = null;
            RefreshHover();
        }
    }

    private void RefreshHover()
    {
        _hoverMarker.Visible = _hoveredCell != null;

        if (_hoveredCell is not Vector2I cell)
        {
            return;
        }

        ItemGrid grid = _getGrid();
        Item held = ItemDragController.Instance.Held;

        if (held == null)
        {
            Item under = grid.At(cell);
            Fit(_hoverMarker, under == null ? cell : grid.AnchorOf(under).Value, under?.GridSize ?? Vector2I.One);
            _hoverMarker.AddThemeStyleboxOverride("panel", Outline(HoverColor));
            return;
        }

        Vector2I anchor = grid.AnchorCenteredOn(held, cell);
        Color color = !grid.CanPlace(held, anchor, out Item displaced) ? BlockedColor
            : displaced == null ? FitsColor
            : SwapsColor;

        Fit(_hoverMarker, anchor, held.GridSize);
        _hoverMarker.AddThemeStyleboxOverride("panel", Fill(color));
    }

    private void RefreshLiftedFrom()
    {
        if (_liftedItem != null && ItemDragController.Instance.Held != _liftedItem)
        {
            _liftedItem = null;
            _liftedGrid = null;
        }

        bool showing = _liftedItem != null && _liftedGrid.IsSameGridAs(_getGrid());
        _liftedFromMarker.Visible = showing;

        if (showing)
        {
            Fit(_liftedFromMarker, _liftedFrom, _liftedItem.GridSize);
        }
    }

    private Control BuildItemVisual(Item item, Vector2I anchor)
    {
        bool usable = _isUsable?.Invoke(item) ?? true;
        Panel panel = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", Fill(usable ? RarityColors.Get(item.Rarity) : UnusableColor));
        Fit(panel, anchor, item.GridSize);

        TextureRect icon = new TextureRect
        {
            Texture = item.Icon,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        icon.SetAnchorsPreset(LayoutPreset.FullRect);
        panel.AddChild(icon);

        return panel;
    }

    private Panel BuildMarker(StyleBox style)
    {
        Panel marker = new Panel { MouseFilter = MouseFilterEnum.Ignore, Visible = false };

        if (style != null)
        {
            marker.AddThemeStyleboxOverride("panel", style);
        }

        AddChild(marker);
        return marker;
    }

    private void Fit(Control control, Vector2I anchor, Vector2I size)
    {
        control.Position = CellPosition(anchor);
        control.Size = FootprintSize(size);
    }

    private Vector2 CellPosition(Vector2I cell) => new Vector2(cell.X, cell.Y) * (CellSize + CellGap);

    public Vector2 FootprintSize(Vector2I size) => new Vector2(size.X, size.Y) * (CellSize + CellGap) - Vector2.One * CellGap;

    private static StyleBoxFlat Fill(Color color)
    {
        StyleBoxFlat style = new StyleBoxFlat { BgColor = color };
        style.SetCornerRadiusAll(4);
        return style;
    }

    private static StyleBoxFlat Outline(Color color)
    {
        StyleBoxFlat style = new StyleBoxFlat { DrawCenter = false, BorderColor = color };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(4);
        return style;
    }
}
