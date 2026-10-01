using Godot;
using SlotHandle = ItemDragController.SlotHandle;

// The hub stash: named tabs of StashTab.SlotCount slots each, beside Inventory like the vendor and
// crafting table. The first tab is free and each extra one costs GameRules.StashTabGoldCost. Slots go
// through ItemDragController like Inventory's, so dragging, tooltips and quick-move all work across the
// two; with the stash open, a quick-move from Inventory lands in the current tab.
public partial class StashPanel : Control
{
    public static StashPanel Instance { get; private set; }

    private const float SlotSize = 88f;
    private const int MaxTabNameLength = 20;
    private static readonly Color HeldOriginColor = new Color(0.85f, 0.75f, 0.15f);

    [Export] public Label GoldLabel { get; private set; }
    [Export] public HBoxContainer TabRow { get; private set; }
    [Export] public LineEdit NameEdit { get; private set; }
    [Export] public GridContainer Grid { get; private set; }

    private Button[] _slotButtons;
    private int _tabIndex;

    private static Godot.Collections.Array<StashTab> Tabs => PlayerProfiles.Local.Data.Stash;
    private StashTab CurrentTab => Tabs[_tabIndex];

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        BuildSlots();
        NameEdit.MaxLength = MaxTabNameLength;
        NameEdit.TextSubmitted += _ => CommitName();
        NameEdit.FocusExited += CommitName;

        ItemDragController.Instance.Changed += RefreshSlots;
        ItemDragController.Instance.RegisterPanelBounds(this);
        ItemDragController.Instance.RegisterQuickMoveTarget(owner: this, isOpen: () => Visible, tryAccept: TryStore);
    }

    public override void _ExitTree()
    {
        ItemDragController.Instance.Changed -= RefreshSlots;
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
        RefreshSlots();
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

    // All the buttons live for the panel's whole life; switching tabs only changes what they show.
    private void BuildSlots()
    {
        _slotButtons = new Button[StashTab.SlotCount];

        for (int i = 0; i < StashTab.SlotCount; i++)
        {
            SlotHandle slot = Slot(i);
            Button button = new Button { CustomMinimumSize = new Vector2(SlotSize, SlotSize) };

            TextureRect icon = new TextureRect
            {
                Name = "Icon",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            };
            icon.SetAnchorsPreset(LayoutPreset.FullRect);
            button.AddChild(icon);

            button.Pressed += () => OnSlotPressed(slot, button);
            button.GuiInput += @event => ItemDragController.Instance.OnSlotGuiInput(slot, button, @event);
            ItemDragController.Instance.RegisterSlot(button, slot);
            ItemTooltip.Instance.Register(button, () => slot.GetItem());

            Grid.AddChild(button);
            _slotButtons[i] = button;
        }
    }

    // Reads the current tab on every call, so one handle per button stays right across tab switches.
    private SlotHandle Slot(int index) => new SlotHandle(
        getItem: () => CurrentTab.Items[index],
        placeItem: item => CurrentTab.Items[index] = item,
        canAccept: item => true);

    private void OnSlotPressed(SlotHandle slot, Button button)
    {
        if (ItemDragController.Instance.ConsumeDragJustHandled())
        {
            return;
        }

        ItemDragController.Instance.OnSlotPressed(slot, button);
    }

    // The current tab's first empty slot. Never from the stash into itself - a quick-move from a stash
    // slot falls through to Inventory instead.
    private bool TryStore(Item item)
    {
        int emptySlot = CurrentTab.Items.IndexOf(null);

        if (CurrentTab.Items.Contains(item) || emptySlot < 0)
        {
            return false;
        }

        CurrentTab.Items[emptySlot] = item;
        return true;
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
        RefreshSlots();
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

    private void RefreshSlots()
    {
        if (!Visible)
        {
            return;
        }

        for (int i = 0; i < _slotButtons.Length; i++)
        {
            Button button = _slotButtons[i];
            Item item = CurrentTab.Items[i];
            button.GetNode<TextureRect>("Icon").Texture = item?.Icon;

            if (item != null)
            {
                ItemSlotBackground.Set(button, RarityColors.Get(item.Rarity));
            }
            else if (ItemDragController.Instance.HeldOriginButton == button)
            {
                ItemSlotBackground.Set(button, HeldOriginColor);
            }
            else
            {
                ItemSlotBackground.Clear(button);
            }
        }
    }

    // The grid's right edge steps into Inventory, and Inventory's left edge back here - separate control
    // trees, which Godot's automatic focus search doesn't cross.
    private void LinkInventoryEdge()
    {
        Control entry = InventoryPanel.Instance.LeftEntry;

        for (int i = Grid.Columns - 1; i < _slotButtons.Length; i += Grid.Columns)
        {
            _slotButtons[i].FocusNeighborRight = _slotButtons[i].GetPathTo(entry);
        }

        InventoryPanel.Instance.LinkLeftPanel(_slotButtons[0]);
    }
}
