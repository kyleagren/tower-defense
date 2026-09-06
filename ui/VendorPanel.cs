using Godot;

// The vendor. Two tabs - items currently for sale, and a buyback list of whatever's been sold
// this hub visit - both regenerated fresh every time the hub loads (nothing here persists across
// a hub departure/return, on purpose - see the crafting slot's own README notes on why that's
// NOT true for crash-sensitive state, and why it's fine here). This first pass only builds and
// displays the two lists - buying, selling, and buying back come in later steps.
public partial class VendorPanel : Control
{
    public static VendorPanel Instance { get; private set; }

    private const int StockSize = 5;

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

        RefreshForSale();
        RefreshBuyback();
    }

    public void Open()
    {
        if (!InventoryPanel.Instance.Visible)
        {
            InventoryPanel.Instance.Open();
        }

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

    private void RefreshForSale()
    {
        ClearChildren(ForSaleList);

        foreach (Item item in _stock)
        {
            ForSaleList.AddChild(BuildItemRow(item));
        }
    }

    private void RefreshBuyback()
    {
        ClearChildren(BuybackList);

        foreach (Item item in _buyback)
        {
            BuybackList.AddChild(BuildItemRow(item));
        }
    }

    private static void ClearChildren(Control container)
    {
        foreach (Node child in container.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static Button BuildItemRow(Item item)
    {
        return new Button
        {
            Text = $"{item.Name}  —  {item.SaleValue}g",
            Icon = item.Icon,
            TooltipText = item.BuildTooltip(),
            CustomMinimumSize = new Vector2(0, 48)
        };
    }
}
