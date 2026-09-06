using Godot;

// The crafting table. This first pass only wires up the item slot itself, sharing
// ItemDragController with InventoryPanel so an item can be dragged in and back out - the actual
// add/remove/reroll options come in a later pass, once dragging is confirmed working end to end.
public partial class CraftingPanel : Control
{
    public static CraftingPanel Instance { get; private set; }

    [Export] public Button ItemSlotButton { get; private set; }

    private ItemDragController.SlotHandle _slot;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        _slot = new ItemDragController.SlotHandle(
            getItem: () => SaveManager.Instance.Data.CraftingSlotItem,
            placeItem: item => SaveManager.Instance.Data.CraftingSlotItem = item,
            canAccept: item => true);

        ItemDragController.Instance.RegisterSlot(ItemSlotButton, _slot);
        ItemSlotButton.Pressed += OnSlotPressed;
        ItemSlotButton.GuiInput += @event => ItemDragController.Instance.OnSlotGuiInput(_slot, ItemSlotButton, @event);
        ItemDragController.Instance.Changed += RefreshSlotVisual;
    }

    public override void _ExitTree()
    {
        ItemDragController.Instance.Changed -= RefreshSlotVisual;
    }

    private void OnSlotPressed()
    {
        if (ItemDragController.Instance.ConsumeDragJustHandled())
        {
            return;
        }

        ItemDragController.Instance.OnSlotPressed(_slot);
    }

    public void Open()
    {
        if (!InventoryPanel.Instance.Visible)
        {
            InventoryPanel.Instance.Open();
        }

        RefreshSlotVisual();
        Visible = true;
        UiPanelStack.Push(this);
    }

    private void RefreshSlotVisual()
    {
        Item item = SaveManager.Instance.Data.CraftingSlotItem;
        ItemSlotButton.Icon = item?.Icon;
        ItemSlotButton.Text = item == null ? "Empty" : "";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !UiPanelStack.IsTop(this))
        {
            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Close()
    {
        Visible = false;
        UiPanelStack.Pop(this);
    }
}
