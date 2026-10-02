using Godot;

// The hub stash: named tabs, each a grid like Inventory's, beside Inventory like the vendor and
// crafting table. The first tab is free and each extra one costs GameRules.StashTabGoldCost. One
// ItemGridView shows whichever tab is current, so dragging, tooltips and quick-move all work across the
// two panels; with the stash open, a quick-move from Inventory lands in the current tab.
public partial class StashPanel : Control
{
    public static StashPanel Instance { get; private set; }

    private const int MaxTabNameLength = 20;

    [Export] public Label GoldLabel { get; private set; }
    [Export] public HBoxContainer TabRow { get; private set; }
    [Export] public LineEdit NameEdit { get; private set; }
    [Export] public ItemGridView Grid { get; private set; }

    private int _tabIndex;

    private static Godot.Collections.Array<StashTab> Tabs => PlayerProfiles.Local.Data.Stash;
    private StashTab CurrentTab => Tabs[_tabIndex];

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        Grid.Setup(() => CurrentTab.Grid, isUsable: null);
        NameEdit.MaxLength = MaxTabNameLength;
        NameEdit.TextSubmitted += _ => CommitName();
        NameEdit.FocusExited += CommitName;

        ItemDragController.Instance.RegisterPanelBounds(this);
        ItemDragController.Instance.RegisterQuickMoveTarget(owner: this, isOpen: () => Visible, tryAccept: TryStore);
    }

    public void Open()
    {
        if (!InventoryPanel.Instance.Visible)
        {
            InventoryPanel.Instance.Open();
        }

        Visible = true;
        _tabIndex = Mathf.Clamp(_tabIndex, 0, Tabs.Count - 1);
        RebuildTabs();
        Grid.Refresh();
        LinkInventoryEdge();
        UiPanelStack.Push(this, Close);
    }

    private void Close()
    {
        CommitName();
        InventoryPanel.Instance.LinkLeftPanel(null);
        Visible = false;
        UiPanelStack.Pop(this);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && UiPanelStack.IsTop(this) && @event.IsActionPressed("ui_cancel"))
        {
            UiPanelStack.CloseAll();
            GetViewport().SetInputAsHandled();
        }
    }

    // The current tab's first space it fits. Never from the stash into itself - a quick-move from the
    // stash falls through to Inventory instead.
    private bool TryStore(Item item)
    {
        ItemGrid grid = CurrentTab.Grid;
        return !grid.Contains(item) && grid.TryAdd(item);
    }

    // Rebuilt only when the tabs themselves change - not on every item move, which would take focus
    // off a tab button a controller is sitting on.
    private void RebuildTabs()
    {
        foreach (Node child in TabRow.GetChildren())
        {
            TabRow.RemoveChild(child);
            child.QueueFree();
        }

        for (int i = 0; i < Tabs.Count; i++)
        {
            int index = i;
            Button tab = new Button { Text = Tabs[i].Name, ToggleMode = true, ButtonPressed = i == _tabIndex };
            tab.Pressed += () => SelectTab(index);
            TabRow.AddChild(tab);
        }

        Button buy = new Button
        {
            Text = $"+ Tab ({GameRules.StashTabGoldCost}g)",
            Disabled = PlayerProfiles.Local.Data.Gold < GameRules.StashTabGoldCost
        };
        buy.Pressed += BuyTab;
        TabRow.AddChild(buy);

        GoldLabel.Text = $"Gold: {PlayerProfiles.Local.Data.Gold}";
        NameEdit.Text = CurrentTab.Name;
    }

    private void SelectTab(int index)
    {
        CommitName();
        _tabIndex = index;
        RebuildTabs();
        Grid.Refresh();
    }

    private void BuyTab()
    {
        if (PlayerProfiles.Local.TryBuyStashTab())
        {
            SelectTab(Tabs.Count - 1);
        }
    }

    // A blank name falls back to the tab's number, so no tab is ever unlabelled.
    private void CommitName()
    {
        string name = NameEdit.Text.StripEdges();

        if (name == "")
        {
            name = $"Tab {_tabIndex + 1}";
        }

        if (name == CurrentTab.Name)
        {
            return;
        }

        PlayerProfiles.Local.RenameStashTab(_tabIndex, name);

        if (Visible)
        {
            RebuildTabs();
        }
    }

    // The grid's right edge steps into Inventory, and Inventory's left edge back here - separate control
    // trees, which Godot's automatic focus search doesn't cross.
    private void LinkInventoryEdge()
    {
        Control entry = InventoryPanel.Instance.LeftEntry;

        for (int row = 0; row < Grid.Rows; row++)
        {
            Button cell = Grid.Cell(new Vector2I(Grid.Columns - 1, row));
            cell.FocusNeighborRight = cell.GetPathTo(entry);
        }

        InventoryPanel.Instance.LinkLeftPanel(Grid.Cell(Vector2I.Zero));
    }
}
