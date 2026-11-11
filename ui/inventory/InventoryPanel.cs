using Godot;
using System;
using System.Collections.Generic;
using SlotHandle = ItemDragController.SlotHandle;

public partial class InventoryPanel : Control, IHintSource
{
    public static InventoryPanel Instance { get; private set; }

    private static readonly Color AcceptBorderColor = new Color(0.3f, 0.9f, 0.3f);
    private static readonly Color PlaceholderTint = new Color(1f, 1f, 1f, 0.4f);
    private static readonly Color HeldOriginColor = new Color(0.85f, 0.75f, 0.15f);
    private const int AcceptBorderWidth = 3;
    private const float DollGap = 12f;

    // Each equipment slot's focus neighbor per direction (left, right, up, down), by
    // _equipmentSlots index. The silhouette is too sparse for Godot's automatic nearest-control
    // pick - from Gloves, a grid square is nearer to the right than Boots. Stay holds focus at an
    // edge; Auto leaves it to Godot, used only for the bottom row's step down into the grid.
    private const int Stay = -1;
    private const int Auto = -2;

    private const int WeaponSlot = 2;
    private const int LeftRingSlot = 5;
    private const int GlovesSlot = 9;
    private const int BootsSlot = 10;

    private static readonly int[,] _equipmentFocusNeighbors =
    {
        { 2, 1, Stay, 3 },       // Helmet
        { 0, 4, Stay, 7 },       // Amulet
        { Stay, 3, Stay, 5 },    // Weapon
        { 2, 4, 0, 6 },          // Chest
        { 3, Stay, 1, 7 },       // Relic
        { Stay, 6, 2, 9 },       // Left Ring
        { 5, 7, 3, 8 },          // Belt
        { 6, Stay, 1, 10 },      // Right Ring
        { 9, 10, 6, Auto },      // Legplates
        { Stay, 10, 8, Auto },   // Gloves
        { 9, Stay, 8, Auto }     // Boots
    };

    // One equipment slot's button, its empty-state placeholder name, and how to read/write the
    // EquippedLoadout field behind it. Built once in _Ready so the click handlers and the visual
    // refresh share one description of the eleven slots rather than two lists kept in lockstep.
    private readonly struct EquipmentSlotView
    {
        public readonly Button Button;
        public readonly string PlaceholderName;
        public readonly SlotHandle Slot;

        public EquipmentSlotView(Button button, string placeholderName, SlotHandle slot)
        {
            Button = button;
            PlaceholderName = placeholderName;
            Slot = slot;
        }
    }

    [Export] public Control SlotArea { get; private set; }
    [Export] public ItemGridView Grid { get; private set; }
    [Export] public Control PhaseIndicator { get; private set; }

    private EquipmentSlotView[] _equipmentSlots;
    private Control _doll;

    private EquippedLoadout _loadout;
    private Control _leftPanelEntry;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        BuildEquipmentSlots();
        BuildGrid();

        // Sit just below PhaseIndicator when it's showing, rather than a hardcoded number that could
        // drift out of sync with it - CallDeferred because a Control's Size isn't settled until after
        // the first layout pass.
        if (PhaseIndicator != null)
        {
            PhaseIndicator.Resized += UpdateTopOffset;
            PhaseIndicator.VisibilityChanged += UpdateTopOffset;
            CallDeferred(nameof(UpdateTopOffset));
        }

        EventBus.Instance.InventoryChanged += OnInventoryChangedExternally;
        ItemDragController.Instance.Changed += OnInventoryChangedExternally;
        ItemDragController.Instance.RegisterPanelBounds(this);
        ItemDragController.Instance.RegisterInventoryFallback(item =>
            !PlayerProfiles.Local.Data.InventoryGrid.Contains(item) && PlayerProfiles.Local.TryAddToInventory(item));
    }

    public override void _ExitTree()
    {
        EventBus.Instance.InventoryChanged -= OnInventoryChangedExternally;
        ItemDragController.Instance.Changed -= OnInventoryChangedExternally;
    }

    // Order matters only in that it's the index _equipmentFocusNeighbors uses.
    private void BuildEquipmentSlots()
    {
        Dictionary<EquipmentSlot, Rect2> layout = DollLayout(out Vector2 dollSize);

        _doll = new Control { MouseFilter = MouseFilterEnum.Ignore };
        ControlLayout.CenterOnAnchor(_doll, new Vector2(0.5f, 0.5f), dollSize);
        SlotArea.CustomMinimumSize = new Vector2(0f, dollSize.Y);
        SlotArea.AddChild(_doll);

        _equipmentSlots = new[]
        {
            CreateEquipmentSlot("Helmet", layout[EquipmentSlot.Helmet], SimpleSlot(() => _loadout.Helmet, item => _loadout.Helmet = (Helmet)item)),
            CreateEquipmentSlot("Amulet", layout[EquipmentSlot.Amulet], SimpleSlot(() => _loadout.Amulet, item => _loadout.Amulet = (Amulet)item)),
            CreateEquipmentSlot("Weapon", layout[EquipmentSlot.Weapon], SimpleSlot(() => _loadout.Weapon, item => _loadout.Weapon = (Weapon)item)),
            CreateEquipmentSlot("Chest", layout[EquipmentSlot.Chest], SimpleSlot(() => _loadout.Chest, item => _loadout.Chest = (Chest)item)),
            CreateEquipmentSlot("Relic", layout[EquipmentSlot.Relic], SimpleSlot(() => _loadout.Relic, item => _loadout.Relic = (Relic)item)),
            CreateEquipmentSlot("Ring", layout[EquipmentSlot.LeftRing], SimpleSlot(() => _loadout.LeftRing, item => _loadout.LeftRing = (Ring)item)),
            CreateEquipmentSlot("Belt", layout[EquipmentSlot.Belt], SimpleSlot(() => _loadout.Belt, item => _loadout.Belt = (Belt)item)),
            CreateEquipmentSlot("Ring", layout[EquipmentSlot.RightRing], SimpleSlot(() => _loadout.RightRing, item => _loadout.RightRing = (Ring)item)),
            CreateEquipmentSlot("Legplates", layout[EquipmentSlot.Legplates], SimpleSlot(() => _loadout.Legplates, item => _loadout.Legplates = (Legplates)item)),
            CreateEquipmentSlot("Gloves", layout[EquipmentSlot.Gloves], SimpleSlot(() => _loadout.Gloves, item => _loadout.Gloves = (Gloves)item)),
            CreateEquipmentSlot("Boots", layout[EquipmentSlot.Boots], SimpleSlot(() => _loadout.Boots, item => _loadout.Boots = (Boots)item))
        };

        LinkEquipmentFocus();
    }

    // The paper doll at Inventory's own cell size, so an item looks the same size on and off: Helmet,
    // Chest, Belt and Legplates down the middle; Weapon, a Ring and Gloves down the left; Amulet,
    // Relic, the other Ring and Boots down the right. Side items line up with their neighbor in the
    // middle, hugging it horizontally, with Gloves and Boots level with the bottom of Legplates.
    private Dictionary<EquipmentSlot, Rect2> DollLayout(out Vector2 dollSize)
    {
        Vector2 Size(EquipmentSlot slot) => Grid.FootprintSize(Item.GridSizeOf(slot));

        float sideWidth = Size(EquipmentSlot.Weapon).X;
        float middleX = sideWidth + DollGap;
        float middleWidth = Size(EquipmentSlot.Chest).X;
        float rightX = middleX + middleWidth + DollGap;

        float chestY = Size(EquipmentSlot.Helmet).Y + DollGap;
        float beltY = chestY + Size(EquipmentSlot.Chest).Y + DollGap;
        float legplatesY = beltY + Size(EquipmentSlot.Belt).Y + DollGap;
        float bottom = legplatesY + Size(EquipmentSlot.Legplates).Y;

        Dictionary<EquipmentSlot, Rect2> layout = new();

        void Middle(EquipmentSlot slot, float y) => layout[slot] = new Rect2(middleX + (middleWidth - Size(slot).X) / 2f, y, Size(slot));
        void Left(EquipmentSlot slot, float y) => layout[slot] = new Rect2(sideWidth - Size(slot).X, y, Size(slot));
        void Right(EquipmentSlot slot, float y) => layout[slot] = new Rect2(rightX, y, Size(slot));

        Middle(EquipmentSlot.Helmet, 0f);
        Middle(EquipmentSlot.Chest, chestY);
        Middle(EquipmentSlot.Belt, beltY);
        Middle(EquipmentSlot.Legplates, legplatesY);

        Left(EquipmentSlot.Weapon, 0f);
        Left(EquipmentSlot.LeftRing, beltY);
        Left(EquipmentSlot.Gloves, bottom - Size(EquipmentSlot.Gloves).Y);

        Right(EquipmentSlot.Amulet, (Size(EquipmentSlot.Helmet).Y - Size(EquipmentSlot.Amulet).Y) / 2f);
        Right(EquipmentSlot.Relic, chestY);
        Right(EquipmentSlot.RightRing, beltY);
        Right(EquipmentSlot.Boots, bottom - Size(EquipmentSlot.Boots).Y);

        dollSize = new Vector2(rightX + sideWidth, bottom);
        return layout;
    }

    private void LinkEquipmentFocus()
    {
        for (int i = 0; i < _equipmentSlots.Length; i++)
        {
            Button button = _equipmentSlots[i].Button;
            button.FocusNeighborLeft = EquipmentNeighborPath(button, _equipmentFocusNeighbors[i, 0]);
            button.FocusNeighborRight = EquipmentNeighborPath(button, _equipmentFocusNeighbors[i, 1]);
            button.FocusNeighborTop = EquipmentNeighborPath(button, _equipmentFocusNeighbors[i, 2]);
            button.FocusNeighborBottom = EquipmentNeighborPath(button, _equipmentFocusNeighbors[i, 3]);
        }
    }

    private NodePath EquipmentNeighborPath(Button from, int target) => target switch
    {
        Auto => new NodePath(),
        Stay => from.GetPathTo(from),
        _ => from.GetPathTo(_equipmentSlots[target].Button)
    };

    // The slot's accessors read _loadout through this panel rather than capturing it, so a handle
    // built once here stays correct after Refresh swaps in a different character's loadout.
    private EquipmentSlotView CreateEquipmentSlot(string placeholderName, Rect2 rect, SlotHandle slot)
    {
        Button button = BuildSlotButton(rect);
        button.Pressed += () => OnEquipmentSlotPressed(slot, button);
        button.GuiInput += @event => ItemDragController.Instance.OnSlotGuiInput(slot, button, @event);
        button.GuiInput += @event => OnEquipmentSlotGuiInput(slot, @event);
        ItemDragController.Instance.RegisterSlot(button, slot);
        ItemTooltip.Instance.Register(button, () => slot.GetItem());

        return new EquipmentSlotView(button, placeholderName, slot);
    }

    private void BuildGrid()
    {
        Grid.Setup(() => PlayerProfiles.Local.Data.InventoryGrid, IsCompatible);
        Grid.TryHandlePress = TryQuickEquipPress;
        Grid.CellGuiInput += OnGridCellGuiInput;
        LinkGridTopRowFocus();
    }

    // Up from the grid's top row lands on Gloves or Boots, whichever sits above that half - never
    // Legplates, which Godot's nearest pick would reach from the middle columns.
    private void LinkGridTopRowFocus()
    {
        for (int column = 0; column < Grid.Columns; column++)
        {
            Button cell = Grid.Cell(new Vector2I(column, 0));
            int target = column < Grid.Columns / 2 ? GlovesSlot : BootsSlot;
            cell.FocusNeighborTop = cell.GetPathTo(_equipmentSlots[target].Button);
        }
    }

    // Where focus enters this panel from a side panel on its left.
    public Control LeftEntry => _equipmentSlots[WeaponSlot].Button;

    // A side panel opened alongside this one (crafting, vendor) is a separate control tree, which
    // Godot's automatic focus search doesn't cross - so the left edge is linked to it by hand, and
    // reset to holding focus when it closes (target null).
    public void LinkLeftPanel(Control target)
    {
        _leftPanelEntry = target;

        foreach (int slot in new[] { WeaponSlot, LeftRingSlot, GlovesSlot })
        {
            Button button = _equipmentSlots[slot].Button;
            button.FocusNeighborLeft = button.GetPathTo(target ?? button);
        }

        for (int row = 0; row < Grid.Rows; row++)
        {
            Button cell = Grid.Cell(new Vector2I(0, row));
            cell.FocusNeighborLeft = cell.GetPathTo(target ?? cell);
        }
    }

    private void OnInventoryChangedExternally()
    {
        if (Visible)
        {
            Refresh();
        }
    }

    private void UpdateTopOffset()
    {
        OffsetTop = PhaseIndicator.Visible ? PhaseIndicator.Size.Y + 4f : 0f;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle_inventory"))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        // With a side panel open, the bumpers jump straight between it and this one.
        if (Visible && _leftPanelEntry != null)
        {
            if (@event.IsActionPressed("ability_modifier"))
            {
                _leftPanelEntry.GrabFocus();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event.IsActionPressed("token_modifier"))
            {
                FocusDefaultSlot();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (Visible && UiPanelStack.IsTop(this) && @event.IsActionPressed("ui_cancel"))
        {
            UiPanelStack.CloseAll();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Toggle()
    {
        if (Visible)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (LevelContext.Current?.LocalActiveCharacter == null)
        {
            return;
        }

        Visible = true;
        Refresh();
        FocusDefaultSlot();
        UiPanelStack.Push(this, Close);
    }

    public void FocusDefaultSlot()
    {
        Grid.Cell(Vector2I.Zero).GrabFocus();
    }

    // What the focused slot (controller) or hovered one (mouse) offers. With an item carried, only
    // where it can go.
    public IEnumerable<Hint> GetHints()
    {
        if (ItemDragController.Instance.Held != null)
        {
            if (InputDevice.UsingController)
            {
                yield return new Hint("ui_cancel", "Put Back");
                yield return new Hint("ui_cancel", "Drop", Hold: true);
            }
            else
            {
                yield return new Hint("ui_accept", "Drop", MouseText: "Click Outside");
            }

            yield break;
        }

        Control pointed = InputDevice.UsingController ? GetViewport().GuiGetFocusOwner() : GetViewport().GuiGetHoveredControl();

        if (pointed == null || !IsAncestorOf(pointed) || !ItemDragController.Instance.TryGetSlot(pointed, out SlotHandle slot)
            || slot.GetItem() is not Item item)
        {
            yield break;
        }

        bool equipped = Array.Exists(_equipmentSlots, view => view.Button == pointed);

        if (item is RuneTome)
        {
            yield return new Hint("inventory_use", "Learn", MouseText: "Right Click");
            yield break;
        }

        if (equipped || IsCompatible(item))
        {
            yield return new Hint("inventory_use", equipped ? "Unequip" : "Equip", MouseText: "Right Click");
        }

        yield return new Hint("advanced_tooltip", "Details", Hold: true);

        if (!equipped)
        {
            yield return new Hint("compare_item", "Compare", Hold: true);
        }
    }

    private void Close()
    {
        // Never leave a picked-up item floating outside both the loadout and Inventory.
        Item held = ItemDragController.Instance.TakeHeld();

        if (held != null)
        {
            ReturnToInventory(held);
        }

        Visible = false;
        UiPanelStack.Pop(this);
    }

    private CharacterId ActiveCharacter => PlayerProfiles.Local.SelectedCharacter;

    private void Refresh()
    {
        _loadout = GetOrCreateLoadout();
        RefreshEquipmentSlots();
        Grid.Refresh();
    }

    private EquippedLoadout GetOrCreateLoadout()
    {
        return PlayerProfiles.Local.GetLoadout(ActiveCharacter);
    }

    private void RefreshEquipmentSlots()
    {
        foreach (EquipmentSlotView view in _equipmentSlots)
        {
            SetEquipmentSlotVisual(view);
        }
    }

    // Empty slots show their own name as a faint placeholder, and highlight if the currently-held
    // item could go here. Occupied slots show the item's icon tinted by rarity, name hidden - an
    // empty Label still reserves its own row in the VBoxContainer, squeezing the icon otherwise.
    private void SetEquipmentSlotVisual(EquipmentSlotView view)
    {
        Button button = view.Button;
        Label label = button.GetNode<Label>("Content/Label");
        Item equippedItem = view.Slot.GetItem();
        Item held = ItemDragController.Instance.Held;

        bool canAcceptHeld = held != null && view.Slot.CanAccept(held);

        if (equippedItem != null)
        {
            SetBackground(button, RarityColors.Get(equippedItem.Rarity), canAcceptHeld);
        }
        else
        {
            ClearBackground(button, canAcceptHeld, isHeldOrigin: ItemDragController.Instance.HeldOriginButton == button);
        }

        if (equippedItem != null)
        {
            SetItemVisual(button, equippedItem.Icon, "");
            label.Visible = false;
        }
        else
        {
            SetItemVisual(button, null, view.PlaceholderName);
            label.Visible = true;
            label.Modulate = PlaceholderTint;
        }
    }

    private bool TryQuickEquipPress(Vector2I cell)
    {
        if (ItemDragController.Instance.Held != null || !Input.IsActionPressed("quick_equip"))
        {
            return false;
        }

        QuickEquip(cell);
        return true;
    }

    // inventory_use (right-click / Square) is a cell's one-press action: learn a Rune Tome, or equip
    // anything else straight from the grid.
    private void OnGridCellGuiInput(Vector2I cell, InputEvent @event)
    {
        if (!@event.IsActionPressed("inventory_use"))
        {
            return;
        }

        GetViewport().SetInputAsHandled();

        if (PlayerProfiles.Local.Data.InventoryGrid.At(cell) is RuneTome tome)
        {
            PlayerProfiles.Local.LearnRuneTome(tome);
        }
        else
        {
            QuickEquip(cell);
        }
    }

    private void OnEquipmentSlotGuiInput(SlotHandle slot, InputEvent @event)
    {
        if (!@event.IsActionPressed("inventory_use"))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        QuickUnequip(slot);
    }

    // Equips a grid item straight into its slot. Whatever that slot held takes the item's place in the
    // grid when it fits there, and otherwise goes to the first space it fits.
    private void QuickEquip(Vector2I cell)
    {
        ItemGrid grid = PlayerProfiles.Local.Data.InventoryGrid;
        Item item = grid.At(cell);

        if (item == null || !IsCompatible(item))
        {
            return;
        }

        Vector2I anchor = grid.AnchorOf(item).Value;
        grid.Remove(item);
        Item displaced = _loadout.ForceEquip(item);

        if (displaced != null && grid.CanPlace(displaced, anchor, out Item blocking) && blocking == null)
        {
            grid.Place(displaced, anchor);
        }
        else
        {
            ReturnToInventory(displaced);
        }

        CommitChange();
    }

    // Shift-click a slot to skip the pick-up-then-place flow: an equipped item goes straight back
    // to the earliest open Inventory slot, mirroring QuickEquip's grid-side shortcut in reverse.
    private void OnEquipmentSlotPressed(SlotHandle slot, Control button)
    {
        if (ItemDragController.Instance.ConsumeDragJustHandled())
        {
            return;
        }

        if (ItemDragController.Instance.Held == null && Input.IsActionPressed("quick_equip"))
        {
            QuickUnequip(slot);
            return;
        }

        ItemDragController.Instance.OnSlotPressed(slot, button);
    }

    private void QuickUnequip(SlotHandle slot)
    {
        Item item = slot.GetItem();

        if (item == null)
        {
            return;
        }

        slot.PlaceItem(null);
        ReturnToInventory(item);

        CommitChange();
    }

    // The redraw comes from InventoryChanged, which this panel already listens to while open.
    private static void CommitChange()
    {
        PlayerProfiles.Local.CommitChange();
    }

    // A SlotHandle for an equipment field. Every equipment slot, weapon included, uses this same
    // get/place/accept shape: accepting requires the right item subtype and character compatibility.
    private SlotHandle SimpleSlot<T>(Func<T> getEquipped, Action<Item> assignEquipped) where T : Item
    {
        return SlotHandle.Single(
            getItem: () => getEquipped(),
            setItem: assignEquipped,
            canAccept: candidateItem => candidateItem == null || (candidateItem is T && IsCompatible(candidateItem)));
    }

    // Drops a displaced item into the first space in Inventory it fits. Falls back
    // to dropping it in the world at the player's feet if Inventory is completely full, rather
    // than losing it — mirrors WorldItem.PickUp's own "leave it in the world until there's room"
    // behavior for the same situation.
    public void ReturnToInventory(Item item)
    {
        if (item == null)
        {
            return;
        }

        if (!PlayerProfiles.Local.TryAddToInventory(item))
        {
            LevelContext.Current.WorldItemRoster.DropNearLocalPlayer(item);
        }
    }

    // highlighted draws a green border on top of whatever background applies (rarity tint, or none)
    // rather than replacing it - so a slot holding an item you could swap into still shows what's
    // currently there while you decide, instead of hiding it behind a flat highlight color.
    private static void SetBackground(Button button, Color color, bool highlighted = false)
    {
        StyleBoxFlat background = new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };

        ApplyAcceptBorder(background, highlighted);

        button.AddThemeStyleboxOverride("normal", background);
        button.AddThemeStyleboxOverride("hover", background);
    }

    private static void ClearBackground(Button button, bool highlighted = false, bool isHeldOrigin = false)
    {
        if (isHeldOrigin)
        {
            StyleBoxFlat originFill = new StyleBoxFlat
            {
                BgColor = HeldOriginColor,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6
            };

            button.AddThemeStyleboxOverride("normal", originFill);
            button.AddThemeStyleboxOverride("hover", originFill);
            return;
        }

        if (!highlighted)
        {
            button.RemoveThemeStyleboxOverride("normal");
            button.RemoveThemeStyleboxOverride("hover");
            return;
        }

        StyleBoxFlat background = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };

        ApplyAcceptBorder(background, true);

        button.AddThemeStyleboxOverride("normal", background);
        button.AddThemeStyleboxOverride("hover", background);
    }

    private static void ApplyAcceptBorder(StyleBoxFlat style, bool highlighted)
    {
        if (!highlighted)
        {
            return;
        }

        style.BorderColor = AcceptBorderColor;
        style.BorderWidthTop = AcceptBorderWidth;
        style.BorderWidthBottom = AcceptBorderWidth;
        style.BorderWidthLeft = AcceptBorderWidth;
        style.BorderWidthRight = AcceptBorderWidth;
    }

    private Button BuildSlotButton(Rect2 rect)
    {
        Button button = new Button { Position = rect.Position, Size = rect.Size };
        BuildItemVisual(button);
        _doll.AddChild(button);

        return button;
    }

    private static void BuildItemVisual(Button button)
    {
        button.Text = "";

        VBoxContainer content = new VBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
        content.SetAnchorsPreset(LayoutPreset.FullRect);
        content.AddThemeConstantOverride("separation", 4);

        TextureRect icon = new TextureRect
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        Label label = new Label
        {
            Name = "Label",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };

        content.AddChild(icon);
        content.AddChild(label);
        button.AddChild(content);
    }

    private static void SetItemVisual(Button button, Texture2D icon, string name)
    {
        button.GetNode<TextureRect>("Content/Icon").Texture = icon;
        button.GetNode<Label>("Content/Label").Text = name;
    }

    private bool IsCompatible(Item item)
    {
        return item.IsCompatibleWith(ActiveCharacter);
    }
}
