using Godot;
using System;

// The vendor. Two tabs - items currently for sale, and a buyback list of whatever's been sold
// this hub visit - both regenerated fresh every time the hub loads (nothing here persists across
// a hub departure/return, on purpose - see the crafting slot's own README notes on why that's
// NOT true for crash-sensitive state, and why it's fine here). Buying spends gold and adds the
// item to Inventory; selling and buying back come in later steps.
public partial class VendorPanel : Control
{
    public static VendorPanel Instance { get; private set; }

    private const int StockSize = 5;

    [Export] public Label GoldLabel { get; private set; }
    [Export] public VBoxContainer ForSaleList { get; private set; }
    [Export] public VBoxContainer BuybackList { get; private set; }

    private readonly Godot.Collections.Array<Item> _stock = new();
    private readonly Godot.Collections.Array<Item> _buyback = new();

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        for (int i = 0; i < StockSize; i++)
        {
            _stock.Add(LootGenerator.GenerateRandomDrop());
        }

        ItemDragController.Instance.RegisterSellTarget(this, Sell);
        ItemDragController.Instance.RegisterQuickMoveTarget(
            isOpen: () => Visible,
            tryAccept: item =>
            {
                Sell(item);
                return true;
            });

        Refresh();
    }

    public void Open()
    {
        if (!InventoryPanel.Instance.Visible)
        {
            InventoryPanel.Instance.Open();
        }

        Refresh();
        Visible = true;
        UiPanelStack.Push(this);
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

    // Spends gold and adds the item straight to Inventory (or drops it in the world if Inventory's
    // full - the same fallback ReturnToInventory already uses everywhere else). Immediate Save(),
    // not the debounced RequestSave() - matches the crafting table's own reasoning: a real gold-for-
    // item trade is worth protecting the instant it happens, not a second later.
    private void Buy(Item item)
    {
        if (SaveManager.Instance.Data.Gold < item.SaleValue)
        {
            return;
        }

        SaveManager.Instance.Data.Gold -= item.SaleValue;
        _stock.Remove(item);
        InventoryPanel.Instance.ReturnToInventory(item);

        SaveManager.Instance.Save();
        EventBus.Instance.EmitSignal(EventBus.SignalName.InventoryChanged);
        Refresh();
    }

    // Gold in, item into Buyback for the same price - no markup either way, same "keep it simple
    // for now" call as the crafting table's own gold costs.
    private void Sell(Item item)
    {
        SaveManager.Instance.Data.Gold += item.SaleValue;
        _buyback.Add(item);

        SaveManager.Instance.Save();
        Refresh();
    }

    private void Refresh()
    {
        GoldLabel.Text = $"Gold: {SaveManager.Instance.Data.Gold}";
        RefreshForSale();
        RefreshBuyback();
    }

    private void RefreshForSale()
    {
        ClearChildren(ForSaleList);

        foreach (Item item in _stock)
        {
            Button button = BuildItemRow(item, () => Buy(item));
            button.Disabled = SaveManager.Instance.Data.Gold < item.SaleValue;
            ForSaleList.AddChild(button);
        }
    }

    private void RefreshBuyback()
    {
        ClearChildren(BuybackList);

        foreach (Item item in _buyback)
        {
            BuybackList.AddChild(BuildItemRow(item, () => { }));
        }
    }

    private static void ClearChildren(Control container)
    {
        foreach (Node child in container.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static Button BuildItemRow(Item item, Action onPressed)
    {
        Button button = new Button
        {
            Text = $"{item.Name}  —  {item.SaleValue}g",
            Icon = item.Icon,
            TooltipText = item.BuildTooltip(),
            CustomMinimumSize = new Vector2(0, 48)
        };
        button.Pressed += onPressed;

        return button;
    }
}
