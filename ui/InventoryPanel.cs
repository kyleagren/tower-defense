using Godot;
using System;
using System.Collections.Generic;

public partial class InventoryPanel : Control
{
    private static readonly Color DisabledBg = new Color(0.32f, 0.14f, 0.14f);
    private static readonly Color SelectedBg = new Color(0.20f, 0.45f, 0.20f);
    private static readonly Color PlaceholderTint = new Color(1f, 1f, 1f, 0.4f);

    // A stand-in for "a place an Item can sit" - either a grid index or an equipment field, so
    // OnSlotPressed can treat all 40 grid slots and all 11 equipment slots the same way.
    private readonly struct SlotHandle
    {
        public readonly Func<Item> GetItem;
        public readonly Action<Item> PlaceItem;
        public readonly Func<Item, bool> CanAccept;

        public SlotHandle(Func<Item> getItem, Action<Item> placeItem, Func<Item, bool> canAccept)
        {
            GetItem = getItem;
            PlaceItem = placeItem;
            CanAccept = canAccept;
        }
    }

    // One equipment slot's button, its empty-state placeholder name, and how to read/write the
    // EquippedLoadout field behind it. Built once in _Ready and reused by both the click handlers
    // and the visual refresh - those used to be two separate hand-maintained lists of the same
    // eleven slots, which had to stay in lockstep by hand.
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
    [Export] public TileGrid Grid { get; private set; }
    [Export] public Control PhaseIndicator { get; private set; }
    [Export] public PackedScene WorldItemScene { get; private set; }
    [Export] public float DropDistance = 1f;
    [Export] public float DropDistanceVariance = 0.2f;
    [Export] public float DropAngleSpread = 45f;

    private EquipmentSlotView[] _equipmentSlots;
    private Button[] _gridButtons;

    private EquippedLoadout _loadout;
    private Item _heldItem;
    private TextureRect _cursorIcon;

    private SlotHandle? _dragSourceSlot;
    private Button _dragOriginButton;
    private Vector2 _dragStartPosition;
    private bool _isDragging;
    private bool _dragJustHandled;
    private const float DragThreshold = 8f;

    public override void _Ready()
    {
        BuildEquipmentSlots();
        BuildGrid();

        _cursorIcon = new TextureRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Size = new Vector2(72f, 72f),
            Visible = false
        };
        AddChild(_cursorIcon);

        // Sit just below PhaseIndicator's actual height rather than a hardcoded number that could
        // silently drift out of sync with it - CallDeferred because a Control's Size isn't settled
        // until after the first layout pass, and Resized keeps it correct if that height ever changes.
        // PhaseIndicator is optional - a scene with no phase/economy HUD (the hub) just anchors to
        // the top of the screen instead.
        if (PhaseIndicator != null)
        {
            PhaseIndicator.Resized += UpdateTopOffset;
            CallDeferred(nameof(UpdateTopOffset));
        }

        EventBus.Instance.InventoryChanged += OnInventoryChangedExternally;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.InventoryChanged -= OnInventoryChangedExternally;
    }

    // Fractional positions within SlotArea, roughly a human silhouette: head row (Helmet/Amulet),
    // weapon+torso+relic row, ring+belt row, legs, then gloves/boots at the bottom. Order matters
    // only in that the first entry is what Open() focuses.
    private void BuildEquipmentSlots()
    {
        _equipmentSlots = new[]
        {
            CreateEquipmentSlot("Helmet", new Vector2(0.50f, 0.10f), SimpleSlot(() => _loadout.Helmet, item => _loadout.Helmet = (Helmet)item)),
            CreateEquipmentSlot("Amulet", new Vector2(0.70f, 0.10f), SimpleSlot(() => _loadout.Amulet, item => _loadout.Amulet = (Amulet)item)),
            CreateEquipmentSlot("Weapon", new Vector2(0.08f, 0.32f), SimpleSlot(() => _loadout.Weapon, item => _loadout.Weapon = (Weapon)item)),
            CreateEquipmentSlot("Chest", new Vector2(0.50f, 0.32f), SimpleSlot(() => _loadout.Chest, item => _loadout.Chest = (Chest)item)),
            CreateEquipmentSlot("Relic", new Vector2(0.92f, 0.32f), SimpleSlot(() => _loadout.Relic, item => _loadout.Relic = (Relic)item)),
            CreateEquipmentSlot("Ring", new Vector2(0.30f, 0.54f), SimpleSlot(() => _loadout.LeftRing, item => _loadout.LeftRing = (Ring)item)),
            CreateEquipmentSlot("Belt", new Vector2(0.50f, 0.54f), SimpleSlot(() => _loadout.Belt, item => _loadout.Belt = (Belt)item)),
            CreateEquipmentSlot("Ring", new Vector2(0.70f, 0.54f), SimpleSlot(() => _loadout.RightRing, item => _loadout.RightRing = (Ring)item)),
            CreateEquipmentSlot("Legs", new Vector2(0.50f, 0.72f), SimpleSlot(() => _loadout.Legs, item => _loadout.Legs = (Legs)item)),
            CreateEquipmentSlot("Gloves", new Vector2(0.30f, 0.90f), SimpleSlot(() => _loadout.Gloves, item => _loadout.Gloves = (Gloves)item)),
            CreateEquipmentSlot("Boots", new Vector2(0.70f, 0.90f), SimpleSlot(() => _loadout.Boots, item => _loadout.Boots = (Boots)item))
        };
    }

    // The slot's accessors read _loadout through this panel rather than capturing it, so a handle
    // built once here stays correct after Refresh swaps in a different character's loadout.
    private EquipmentSlotView CreateEquipmentSlot(string placeholderName, Vector2 anchor, SlotHandle slot)
    {
        Button button = BuildSlotButton(anchor);
        button.Pressed += () => OnEquipmentSlotPressed(slot);
        button.GuiInput += @event => OnSlotGuiInput(slot, button, @event);

        return new EquipmentSlotView(button, placeholderName, slot);
    }

    // All 40 slots exist as buttons for the panel's whole life - Inventory's index is the slot, not
    // a filtered view of "whatever's currently owned," so the button set never changes. Only their
    // contents do, which is what RefreshGrid updates.
    private void BuildGrid()
    {
        Godot.Collections.Array<Item> inventory = SaveManager.Instance.Data.Inventory;
        _gridButtons = new Button[inventory.Count];

        for (int i = 0; i < inventory.Count; i++)
        {
            int slotIndex = i;

            Button button = new Button
            {
                CustomMinimumSize = new Vector2(90f, 90f)
            };

            BuildIconOnlyVisual(button);
            button.Pressed += () => OnGridSlotPressed(slotIndex);
            button.GuiInput += @event => OnSlotGuiInput(GridSlot(slotIndex), button, @event);

            Grid.AddChild(button);
            _gridButtons[i] = button;
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
        OffsetTop = PhaseIndicator.Size.Y + 4f;
    }

    public override void _Process(double delta)
    {
        if (_heldItem == null)
        {
            return;
        }

        _cursorIcon.Position = GetLocalMousePosition() - _cursorIcon.Size / 2f;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle_inventory"))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && UiPanelStack.IsTop(this) && @event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_heldItem != null && Visible && UiPanelStack.IsTop(this)
            && @event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            DropInWorld(_heldItem);
            _heldItem = null;
            SaveManager.Instance.RequestSave();
            UpdateCursorIcon();
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

    private void Open()
    {
        if (PlayerCharacter.ActiveCombatant == null)
        {
            return;
        }

        Visible = true;
        Refresh();
        _equipmentSlots[0].Button.GrabFocus();
        UiPanelStack.Push(this);
    }

    private void Close()
    {
        // Never leave a picked-up item floating outside both the loadout and Inventory.
        if (_heldItem != null)
        {
            ReturnToInventory(_heldItem);
            _heldItem = null;
            SaveManager.Instance.RequestSave();
            EventBus.Instance.EmitSignal(EventBus.SignalName.InventoryChanged);
        }

        Visible = false;
        UiPanelStack.Pop(this);
    }

    private CharacterId ActiveCharacter => PlayerSentinels.Get(PlayerSentinels.Local);

    private void Refresh()
    {
        _loadout = GetOrCreateLoadout();
        RefreshEquipmentSlots();
        RefreshGrid();
        UpdateCursorIcon();
    }

    // Keeps the floating cursor icon in sync with _heldItem - shown only while something's picked up.
    private void UpdateCursorIcon()
    {
        _cursorIcon.Visible = _heldItem != null;
        _cursorIcon.Texture = _heldItem?.Icon;
    }

    private EquippedLoadout GetOrCreateLoadout()
    {
        return SaveManager.Instance.Data.GetOrCreateLoadout(ActiveCharacter);
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

        button.TooltipText = _heldItem == null && equippedItem != null ? equippedItem.BuildTooltip() : "";

        if (_heldItem != null && view.Slot.CanAccept(_heldItem))
        {
            SetBackground(button, SelectedBg);
        }
        else if (equippedItem != null)
        {
            SetBackground(button, RarityColors.Get(equippedItem.Rarity));
        }
        else
        {
            ClearBackground(button);
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

    // Updates the existing buttons in place. This runs on every click in the panel, so rebuilding
    // all 40 nodes here (which it used to do) meant a full teardown/instantiate cycle per click.
    private void RefreshGrid()
    {
        Godot.Collections.Array<Item> inventory = SaveManager.Instance.Data.Inventory;

        for (int i = 0; i < _gridButtons.Length; i++)
        {
            Button button = _gridButtons[i];
            Item slotItem = i < inventory.Count ? inventory[i] : null;

            SetIconOnlyVisual(button, slotItem?.Icon);

            if (slotItem == null)
            {
                ClearBackground(button);
                button.TooltipText = "";
                continue;
            }

            SetBackground(button, IsCompatible(slotItem) ? RarityColors.Get(slotItem.Rarity) : DisabledBg);
            button.TooltipText = _heldItem == null ? slotItem.BuildTooltip() : "";
        }
    }

    private void OnGridSlotPressed(int gridIndex)
    {
        if (_dragJustHandled)
        {
            _dragJustHandled = false;
            return;
        }

        bool shiftHeld = _heldItem == null
            && SaveManager.Instance.Data.Inventory[gridIndex] != null
            && Input.IsKeyPressed(Key.Shift);

        if (shiftHeld)
        {
            QuickEquip(gridIndex);
            return;
        }

        OnSlotPressed(GridSlot(gridIndex));
    }

    // Shift-click a grid item to equip it directly, skipping the click-to-pick-up-then-place flow.
    // Whatever was already in the matching slot (if anything) swaps into the grid slot being vacated.
    private void QuickEquip(int gridIndex)
    {
        Godot.Collections.Array<Item> inventory = SaveManager.Instance.Data.Inventory;
        Item item = inventory[gridIndex];

        if (item == null || !IsCompatible(item))
        {
            return;
        }

        Item displaced = _loadout.ForceEquip(item);
        inventory[gridIndex] = displaced;

        CommitChange();
    }

    // Shift-click a slot to skip the pick-up-then-place flow: an equipped item goes straight back
    // to the earliest open Inventory slot, mirroring QuickEquip's grid-side shortcut in reverse.
    private void OnEquipmentSlotPressed(SlotHandle slot)
    {
        if (_dragJustHandled)
        {
            _dragJustHandled = false;
            return;
        }

        if (_heldItem == null && Input.IsKeyPressed(Key.Shift))
        {
            QuickUnequip(slot);
            return;
        }

        OnSlotPressed(slot);
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

    // The single click-handler for every slot in the panel, grid and equipment alike:
    //  - nothing held + click a slot with an item -> pick it up (hold it, clear the slot).
    //  - holding an item + click a slot that accepts it -> place it there; whatever was already
    //    in that slot (if anything) becomes the newly held item, so it's a swap, not a loss.
    //  - holding an item + click a slot that rejects it (wrong type/character) -> do nothing.
    private void OnSlotPressed(SlotHandle slot)
    {
        if (_heldItem != null)
        {
            if (!slot.CanAccept(_heldItem))
            {
                return;
            }

            Item displaced = slot.GetItem();
            slot.PlaceItem(_heldItem);
            _heldItem = displaced;
            CommitChange();
            return;
        }

        Item pickedUpItem = slot.GetItem();

        if (pickedUpItem == null)
        {
            return;
        }

        _heldItem = pickedUpItem;
        slot.PlaceItem(null);
        Refresh();
    }

    // Detects a press-drag-release gesture layered on top of the existing click-click flow. A
    // plain click that never drifts past DragThreshold touches none of this - it falls straight
    // through to Pressed exactly as before, which is why gamepad confirm (which never emits these
    // event types) can't be affected by any of it.
    private void OnSlotGuiInput(SlotHandle slot, Button button, InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                if (_heldItem == null && slot.GetItem() != null)
                {
                    _dragSourceSlot = slot;
                    _dragOriginButton = button;
                    _dragStartPosition = mouseButton.Position;
                    _isDragging = false;
                }

                return;
            }

            if (_isDragging)
            {
                EndDrag();
            }

            _dragSourceSlot = null;
            _dragOriginButton = null;
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion && _dragSourceSlot != null && !_isDragging
            && mouseMotion.Position.DistanceTo(_dragStartPosition) >= DragThreshold)
        {
            BeginDrag();
        }
    }

    // Lifts the item the moment the mouse actually moves, rather than waiting for release - the
    // cursor icon (already driven by _heldItem in _Process) starts following the drag immediately.
    private void BeginDrag()
    {
        _heldItem = _dragSourceSlot.Value.GetItem();
        _dragSourceSlot.Value.PlaceItem(null);
        _isDragging = true;
        Refresh();
    }

    // Finishes a drag - but only for a release somewhere other than the slot it started from. If
    // the cursor ended up back over the origin button, this does nothing: Button.Pressed fires for
    // that release exactly like an ordinary click would, and OnSlotPressed's existing pick-up-then-
    // place logic puts the item right back where it came from.
    private void EndDrag()
    {
        _isDragging = false;
        _dragJustHandled = true;
        CallDeferred(nameof(ClearDragJustHandled));

        Control hovered = GetViewport().GuiGetHoveredControl();

        if (hovered == _dragOriginButton)
        {
            return;
        }

        SlotHandle? targetSlot = FindSlotForControl(hovered);

        if (targetSlot == null)
        {
            DropInWorld(_heldItem);
            _heldItem = null;
            SaveManager.Instance.RequestSave();
            Refresh();
            return;
        }

        if (!targetSlot.Value.CanAccept(_heldItem))
        {
            _dragSourceSlot.Value.PlaceItem(_heldItem);
            _heldItem = null;
            Refresh();
            return;
        }

        Item displaced = targetSlot.Value.GetItem();
        targetSlot.Value.PlaceItem(_heldItem);
        _heldItem = displaced;
        CommitChange();
    }

    private void ClearDragJustHandled()
    {
        _dragJustHandled = false;
    }

    // Maps whatever Control the mouse is over back to the SlotHandle it represents, so a drag
    // release can target it the same way a click would - null if the cursor isn't over a slot at
    // all (background, outside the panel), which EndDrag treats as "drop it in the world" instead.
    private SlotHandle? FindSlotForControl(Control control)
    {
        for (int i = 0; i < _gridButtons.Length; i++)
        {
            if (_gridButtons[i] == control)
            {
                return GridSlot(i);
            }
        }

        foreach (EquipmentSlotView view in _equipmentSlots)
        {
            if (view.Button == control)
            {
                return view.Slot;
            }
        }

        return null;
    }

    // Every mutating path ends the same way: persist, tell the rest of the game a loadout changed
    // (which is what drops the CharacterSheet and every Ritual's cached context), then redraw.
    private void CommitChange()
    {
        SaveManager.Instance.RequestSave();
        EventBus.Instance.EmitSignal(EventBus.SignalName.InventoryChanged);
        Refresh();
    }

    // A SlotHandle for one index of the 40-slot Inventory array. Any item is accepted - the grid
    // has no type restriction of its own, unlike an equipment slot.
    private SlotHandle GridSlot(int index)
    {
        return new SlotHandle(
            getItem: () => SaveManager.Instance.Data.Inventory[index],
            placeItem: newItem => SaveManager.Instance.Data.Inventory[index] = newItem,
            canAccept: candidateItem => true);
    }

    // A SlotHandle for an equipment field. Every equipment slot (weapon included, now that
    // two-handed weapons are gone) uses this same uniform get/place/accept shape: accepting
    // requires the right item subtype and character compatibility.
    private SlotHandle SimpleSlot<T>(Func<T> getEquipped, Action<Item> assignEquipped) where T : Item
    {
        return new SlotHandle(
            getItem: () => getEquipped(),
            placeItem: newItem => assignEquipped(newItem),
            canAccept: candidateItem => candidateItem == null || (candidateItem is T && IsCompatible(candidateItem)));
    }

    // Drops a displaced item into the first empty Inventory slot (lowest index first). Falls back
    // to dropping it in the world at the player's feet if Inventory is completely full, rather
    // than losing it — mirrors WorldItem.PickUp's own "leave it in the world until there's room"
    // behavior for the same situation.
    private void ReturnToInventory(Item item)
    {
        if (item == null)
        {
            return;
        }

        Godot.Collections.Array<Item> inventory = SaveManager.Instance.Data.Inventory;
        int emptySlotIndex = inventory.IndexOf(null);

        if (emptySlotIndex >= 0)
        {
            inventory[emptySlotIndex] = item;
            return;
        }

        DropInWorld(item);
    }

    private void DropInWorld(Item item)
    {
        PlayerCharacter player = PlayerCharacter.ActiveCombatant;
        Vector3 forward = -player.GlobalTransform.Basis.Z;
        Vector3 direction = forward.Rotated(Vector3.Up, Mathf.DegToRad((float)GD.RandRange(-DropAngleSpread, DropAngleSpread)));
        float distance = DropDistance + (float)GD.RandRange(-DropDistanceVariance, DropDistanceVariance);

        WorldItem worldItem = WorldItemScene.Instantiate<WorldItem>();
        worldItem.Initialize(item);
        GetTree().CurrentScene.AddChild(worldItem);
        worldItem.GlobalPosition = player.GlobalPosition + direction * distance;
    }

    private static void SetBackground(Button button, Color color)
    {
        StyleBoxFlat background = new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };

        button.AddThemeStyleboxOverride("normal", background);
        button.AddThemeStyleboxOverride("hover", background);
    }

    private static void ClearBackground(Button button)
    {
        button.RemoveThemeStyleboxOverride("normal");
        button.RemoveThemeStyleboxOverride("hover");
    }

    private Button BuildSlotButton(Vector2 anchor)
    {
        Button button = new Button();
        ControlLayout.CenterOnAnchor(button, anchor, new Vector2(130f, 130f));
        BuildItemVisual(button);
        SlotArea.AddChild(button);

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

    // Grid tiles show only the thumbnail - the name lives in the hover tooltip instead.
    private static void BuildIconOnlyVisual(Button button)
    {
        button.Text = "";

        TextureRect icon = new TextureRect
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        icon.SetAnchorsPreset(LayoutPreset.FullRect);

        button.AddChild(icon);
    }

    private static void SetIconOnlyVisual(Button button, Texture2D icon)
    {
        button.GetNode<TextureRect>("Icon").Texture = icon;
    }

    private bool IsCompatible(Item item)
    {
        return item.IsCompatibleWith(ActiveCharacter);
    }
}
