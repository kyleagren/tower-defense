using Godot;
using System;

// The vendor. Two tabs - items currently for sale, and a buyback list of whatever's been sold
// this hub visit - both regenerated fresh every time the hub loads. Nothing here persists across a
// hub departure/return, deliberately: the hub scene fully reloads each visit, so regenerating falls
// out for free. The crafting slot is the opposite case and does persist, since losing an item
// parked there to a crash would be real loss. Buying and buying back both spend
// gold for an item at its SaleValue; selling (drag onto this panel, or Ctrl+click while it's open)
// pays that same value and moves the item into Buyback - no markup either direction.
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
            owner: this,
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
        LinkInventoryEdge();
        UiPanelStack.Push(this, Close);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !UiPanelStack.IsTop(this))
        {
            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            UiPanelStack.CloseAll();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Close()
    {
        InventoryPanel.Instance.LinkLeftPanel(null);
        Visible = false;
        UiPanelStack.Pop(this);
    }

    // Spends gold and adds the item straight to Inventory (or drops it in the world if Inventory's
    // full - the same fallback ReturnToInventory already uses everywhere else). Immediate Save(),
    // not the debounced RequestSave() - matches the crafting table's own reasoning: a real gold-for-
    // item trade is worth protecting the instant it happens, not a second later. Shared by buying
    // from stock and buying back from the sold list - same transaction, different source list.
    private void Purchase(Item item, Godot.Collections.Array<Item> source)
    {
        PlayerProfile profile = PlayerProfiles.Local;

        if (!profile.TrySpendGold(item.SaleValue))
        {
            return;
        }

        source.Remove(item);
        InventoryPanel.Instance.ReturnToInventory(item);
        profile.CommitChange(saveNow: true);
        Refresh();
    }

    // Gold in, item into Buyback for the same price - no markup either way, same "keep it simple
    // for now" call as the crafting table's own gold costs.
    private void Sell(Item item)
    {
        PlayerProfiles.Local.AddGold(item.SaleValue);
        _buyback.Add(item);
        Refresh();
    }

    // Rows are rebuilt on every refresh, so focus on one would be lost with it - put it back on
    // whatever row now sits at the same position.
    private void Refresh()
    {
        Control focused = GetViewport().GuiGetFocusOwner();
        VBoxContainer focusedList = focused?.GetParent() as VBoxContainer;
        bool hadRowFocus = focusedList == ForSaleList || focusedList == BuybackList;
        int focusedIndex = hadRowFocus ? focused.GetIndex() : -1;

        GoldLabel.Text = $"Gold: {SaveManager.Instance.Data.Gold}";
        RefreshForSale();
        RefreshBuyback();

        if (Visible)
        {
            LinkInventoryEdge();
        }

        if (hadRowFocus)
        {
            RestoreRowFocus(focusedList, focusedIndex);
        }
    }

    // Rebuilt rows need their cross-panel links redone - the old ones point at freed buttons.
    private void LinkInventoryEdge()
    {
        Control entry = InventoryPanel.Instance.LeftEntry;

        foreach (VBoxContainer list in new[] { ForSaleList, BuybackList })
        {
            foreach (Control row in list.GetChildren())
            {
                row.FocusNeighborRight = row.GetPathTo(entry);
            }
        }

        Control first = ForSaleList.GetChildCount() > 0 ? ForSaleList.GetChild<Control>(0)
            : BuybackList.GetChildCount() > 0 ? BuybackList.GetChild<Control>(0)
            : null;
        InventoryPanel.Instance.LinkLeftPanel(first);
    }

    private void RestoreRowFocus(VBoxContainer list, int index)
    {
        VBoxContainer other = list == ForSaleList ? BuybackList : ForSaleList;

        if (list.GetChildCount() > 0)
        {
            list.GetChild<Control>(Mathf.Min(index, list.GetChildCount() - 1)).GrabFocus();
        }
        else if (other.GetChildCount() > 0)
        {
            other.GetChild<Control>(0).GrabFocus();
        }
        else
        {
            InventoryPanel.Instance.FocusDefaultSlot();
        }
    }

    private void RefreshForSale()
    {
        ClearChildren(ForSaleList);

        foreach (Item item in _stock)
        {
            Button button = BuildItemRow(item, () => Purchase(item, _stock));
            button.Disabled = SaveManager.Instance.Data.Gold < item.SaleValue;
            ForSaleList.AddChild(button);
        }
    }

    private void RefreshBuyback()
    {
        ClearChildren(BuybackList);

        foreach (Item item in _buyback)
        {
            Button button = BuildItemRow(item, () => Purchase(item, _buyback));
            button.Disabled = SaveManager.Instance.Data.Gold < item.SaleValue;
            BuybackList.AddChild(button);
        }
    }

    private static void ClearChildren(Control container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static Button BuildItemRow(Item item, Action onPressed)
    {
        Button button = new Button
        {
            Text = $"{item.Name}  —  {item.SaleValue}g",
            Icon = item.Icon,
            CustomMinimumSize = new Vector2(0, 48)
        };
        button.Pressed += onPressed;
        ItemSlotBackground.Set(button, RarityColors.Get(item.Rarity));
        ItemTooltip.Instance.Register(button, () => item);

        return button;
    }
}
