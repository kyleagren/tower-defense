using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// The crafting table. An item in the slot exposes six mod bars (3 prefix, 3 suffix) and a
// scrollable list of crafting actions - add/remove a random prefix or suffix, swap one bar's mod
// for a different random one, reroll every mod's value, or upgrade a random mod's tier. Every
// action costs a flat GameRules.CraftingActionGoldCost, gated by current affix counts and how much
// Gold is banked.
public partial class CraftingPanel : Control
{
    public static CraftingPanel Instance { get; private set; }

    // One entry in the scrollable action list: its label, whether it can currently be performed on
    // the item in the slot, and what performing it does. A plain data list rather than one button
    // per hardcoded field, so adding an action later means adding an entry here rather than wiring up
    // another button by hand.
    private readonly struct CraftingAction
    {
        public readonly string Label;
        public readonly Func<Item, bool> CanPerform;
        public readonly Action<Item> Perform;

        public CraftingAction(string label, Func<Item, bool> canPerform, Action<Item> perform)
        {
            Label = label;
            CanPerform = canPerform;
            Perform = perform;
        }
    }

    private static readonly CraftingAction[] Actions = BuildActions();

    private static CraftingAction[] BuildActions()
    {
        List<CraftingAction> actions = new()
        {
            new("Add Prefix",
                item => CraftingActions.CanAddAffix(item, AffixType.Prefix),
                item => CraftingActions.AddAffix(item, AffixType.Prefix)),
            new("Remove Prefix",
                item => CraftingActions.CanRemoveAffix(item, AffixType.Prefix),
                item => CraftingActions.RemoveRandomAffix(item, AffixType.Prefix)),
            new("Add Suffix",
                item => CraftingActions.CanAddAffix(item, AffixType.Suffix),
                item => CraftingActions.AddAffix(item, AffixType.Suffix)),
            new("Remove Suffix",
                item => CraftingActions.CanRemoveAffix(item, AffixType.Suffix),
                item => CraftingActions.RemoveRandomAffix(item, AffixType.Suffix)),
            new("Reroll All", CraftingActions.CanReroll, CraftingActions.RerollAll),
            new("Upgrade Random Tier", CraftingActions.CanUpgradeTier, CraftingActions.UpgradeRandomTier)
        };

        AddPerBarReplacements(actions, AffixType.Prefix, GameRules.MaxPrefixAffixes);
        AddPerBarReplacements(actions, AffixType.Suffix, GameRules.MaxSuffixAffixes);

        return actions.ToArray();
    }

    private static void AddPerBarReplacements(List<CraftingAction> actions, AffixType type, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int index = i;
            actions.Add(new($"Reroll {type} {index + 1}",
                item => CraftingActions.CanReplaceAffixAt(item, type, index),
                item => CraftingActions.ReplaceAffixAt(item, type, index)));
        }
    }

    private static readonly Color BarBackground = new Color(0.16f, 0.16f, 0.16f);

    [Export] public Button ItemSlotButton { get; private set; }
    [Export] public Label GoldLabel { get; private set; }
    [Export] public VBoxContainer AffixList { get; private set; }
    [Export] public VBoxContainer ActionList { get; private set; }

    private ItemDragController.SlotHandle _slot;
    private PanelContainer[] _prefixBars;
    private PanelContainer[] _suffixBars;
    private Button[] _actionButtons;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        _slot = new ItemDragController.SlotHandle(
            getItem: () => SaveManager.Instance.Data.CraftingSlotItem,
            placeItem: item => PlayerProfiles.Local.SetCraftingSlotItem(item),
            canAccept: item => true);

        ItemDragController.Instance.RegisterSlot(ItemSlotButton, _slot);
        ItemTooltip.Instance.Register(ItemSlotButton, () => _slot.GetItem());
        ItemSlotButton.Pressed += OnSlotPressed;
        ItemSlotButton.GuiInput += @event => ItemDragController.Instance.OnSlotGuiInput(_slot, ItemSlotButton, @event);
        ItemDragController.Instance.Changed += Refresh;
        ItemDragController.Instance.RegisterPanelBounds(this);
        ItemDragController.Instance.RegisterQuickMoveTarget(
            owner: this,
            isOpen: () => Visible,
            tryAccept: item =>
            {
                if (_slot.GetItem() != null || !_slot.CanAccept(item))
                {
                    return false;
                }

                _slot.PlaceItem(item);
                return true;
            });

        AddSectionHeader("Prefixes");
        _prefixBars = BuildBarRow(GameRules.MaxPrefixAffixes);
        AddSectionHeader("Suffixes");
        _suffixBars = BuildBarRow(GameRules.MaxSuffixAffixes);
        BuildActionButtons();
    }

    public override void _ExitTree()
    {
        ItemDragController.Instance.Changed -= Refresh;
    }

    private void OnSlotPressed()
    {
        if (ItemDragController.Instance.ConsumeDragJustHandled())
        {
            return;
        }

        ItemDragController.Instance.OnSlotPressed(_slot, ItemSlotButton);
    }

    public void Open()
    {
        if (!InventoryPanel.Instance.Visible)
        {
            InventoryPanel.Instance.Open();
        }

        Refresh();
        Visible = true;
        InventoryPanel.Instance.LinkLeftPanel(ItemSlotButton);
        LinkRightEdge();
        UiPanelStack.Push(this, Close);
    }

    private void LinkRightEdge()
    {
        Control entry = InventoryPanel.Instance.LeftEntry;
        ItemSlotButton.FocusNeighborRight = ItemSlotButton.GetPathTo(entry);

        foreach (Button button in _actionButtons)
        {
            button.FocusNeighborRight = button.GetPathTo(entry);
        }
    }

    private void AddSectionHeader(string text)
    {
        Label header = new Label { Text = text };
        header.AddThemeFontSizeOverride("font_size", 16);
        AffixList.AddChild(header);
    }

    // Each bar sits in its own dark panel (matching the action buttons' own background) so an empty
    // slot still reads as a reserved mod slot rather than blank space. Content (a mod row or an
    // empty-placeholder label) is filled in by SetBars, since it changes on every refresh.
    private PanelContainer[] BuildBarRow(int count)
    {
        PanelContainer[] bars = new PanelContainer[count];

        for (int i = 0; i < count; i++)
        {
            PanelContainer panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = BarBackground,
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 4,
                ContentMarginBottom = 4,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            });

            AffixList.AddChild(panel);
            bars[i] = panel;
        }

        return bars;
    }

    private void BuildActionButtons()
    {
        _actionButtons = new Button[Actions.Length];

        for (int i = 0; i < Actions.Length; i++)
        {
            CraftingAction action = Actions[i];
            Button button = new Button { Text = $"{action.Label} ({GameRules.CraftingActionGoldCost}g)" };
            button.Pressed += () => OnActionPressed(action);
            ActionList.AddChild(button);
            _actionButtons[i] = button;
        }
    }

    private void OnActionPressed(CraftingAction action)
    {
        PlayerProfile profile = PlayerProfiles.Local;
        Item item = profile.Data.CraftingSlotItem;

        if (item == null || !action.CanPerform(item) || !profile.TrySpendGold(GameRules.CraftingActionGoldCost))
        {
            return;
        }

        action.Perform(item);
        profile.CommitChange(saveNow: true);
        Refresh();
    }

    private void Refresh()
    {
        Item item = SaveManager.Instance.Data.CraftingSlotItem;

        ItemSlotButton.Icon = item?.Icon;
        ItemSlotButton.Text = item == null ? "Empty" : "";

        if (item != null)
        {
            ItemSlotBackground.Set(ItemSlotButton, RarityColors.Get(item.Rarity));
        }
        else
        {
            ItemSlotBackground.Clear(ItemSlotButton);
        }

        SetBars(_prefixBars, item, AffixType.Prefix);
        SetBars(_suffixBars, item, AffixType.Suffix);

        bool canAfford = SaveManager.Instance.Data.Gold >= GameRules.CraftingActionGoldCost;
        for (int i = 0; i < Actions.Length; i++)
        {
            _actionButtons[i].Disabled = item == null || !canAfford || !Actions[i].CanPerform(item);
        }

        GoldLabel.Text = $"Gold: {SaveManager.Instance.Data.Gold}";
    }

    private static void SetBars(PanelContainer[] bars, Item item, AffixType type)
    {
        List<RolledAffix> rolls = item?.Affixes.Where(roll => roll.Affix.AffixType == type).ToList() ?? new List<RolledAffix>();

        for (int i = 0; i < bars.Length; i++)
        {
            // Free() immediately, not QueueFree() - a deferred free would let old and new rows briefly
            // coexist. Same reasoning as ItemTooltip.BuildAdvancedBody.
            foreach (Node child in bars[i].GetChildren())
            {
                child.Free();
            }

            bars[i].AddChild(i < rolls.Count ? AffixRowView.Build(rolls[i]).Root : new Label { Text = $"{type} — Empty" });
        }
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
}
