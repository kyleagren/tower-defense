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

    [Export] public Control SlotArea { get; private set; }
    [Export] public TileGrid Grid { get; private set; }
    [Export] public Control PhaseIndicator { get; private set; }

    private Button _weaponButton;
    private Button _helmetButton;
    private Button _chestButton;
    private Button _legsButton;
    private Button _bootsButton;
    private Button _glovesButton;
    private Button _beltButton;
    private Button _amuletButton;
    private Button _leftRingButton;
    private Button _rightRingButton;
    private Button _relicButton;

    private EquippedLoadout _loadout;
    private Item _heldItem;
    private TextureRect _cursorIcon;

    // Fractional position within SlotArea, roughly a human silhouette: head row (Helmet/Amulet),
    // weapon+torso+relic row, ring+belt row, legs, then gloves/boots at the bottom.
    public override void _Ready()
    {
        _helmetButton = BuildSlotButton(new Vector2(0.50f, 0.10f));
        _amuletButton = BuildSlotButton(new Vector2(0.70f, 0.10f));
        _weaponButton = BuildSlotButton(new Vector2(0.08f, 0.32f));
        _chestButton = BuildSlotButton(new Vector2(0.50f, 0.32f));
        _relicButton = BuildSlotButton(new Vector2(0.92f, 0.32f));
        _leftRingButton = BuildSlotButton(new Vector2(0.30f, 0.54f));
        _beltButton = BuildSlotButton(new Vector2(0.50f, 0.54f));
        _rightRingButton = BuildSlotButton(new Vector2(0.70f, 0.54f));
        _legsButton = BuildSlotButton(new Vector2(0.50f, 0.72f));
        _glovesButton = BuildSlotButton(new Vector2(0.30f, 0.90f));
        _bootsButton = BuildSlotButton(new Vector2(0.70f, 0.90f));

        _weaponButton.Pressed += () => OnSlotPressed(SimpleSlot(() => _loadout.Weapon, newItem => _loadout.Weapon = (Weapon)newItem));
        _helmetButton.Pressed += () => OnSlotPressed(SimpleSlot(() => _loadout.Helmet, newItem => _loadout.Helmet = (Helmet)newItem));
        _chestButton.Pressed += () => OnSlotPressed(SimpleSlot(() => _loadout.Chest, newItem => _loadout.Chest = (Chest)newItem));
        _legsButton.Pressed += () => OnSlotPressed(SimpleSlot(() => _loadout.Legs, newItem => _loadout.Legs = (Legs)newItem));
        _bootsButton.Pressed += () => OnSlotPressed(SimpleSlot(() => _loadout.Boots, newItem => _loadout.Boots = (Boots)newItem));
        _glovesButton.Pressed += () => OnSlotPressed(SimpleSlot(() => _loadout.Gloves, newItem => _loadout.Gloves = (Gloves)newItem));
        _beltButton.Pressed += () => OnSlotPressed(SimpleSlot(() => _loadout.Belt, newItem => _loadout.Belt = (Belt)newItem));
        _amuletButton.Pressed += () => OnSlotPressed(SimpleSlot(() => _loadout.Amulet, newItem => _loadout.Amulet = (Amulet)newItem));
        _leftRingButton.Pressed += () => OnSlotPressed(SimpleSlot(() => _loadout.LeftRing, newItem => _loadout.LeftRing = (Ring)newItem));
        _rightRingButton.Pressed += () => OnSlotPressed(SimpleSlot(() => _loadout.RightRing, newItem => _loadout.RightRing = (Ring)newItem));
        _relicButton.Pressed += () => OnSlotPressed(SimpleSlot(() => _loadout.Relic, newItem => _loadout.Relic = (Relic)newItem));

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
        PhaseIndicator.Resized += UpdateTopOffset;
        CallDeferred(nameof(UpdateTopOffset));
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
        _helmetButton.GrabFocus();
        UiPanelStack.Push(this);
    }

    private void Close()
    {
        // Never leave a picked-up item floating outside both the loadout and Inventory.
        if (_heldItem != null)
        {
            ReturnToInventory(_heldItem);
            _heldItem = null;
            SaveManager.Instance.Save();
        }

        Visible = false;
        UiPanelStack.Pop(this);
    }

    private CharacterId ActiveCharacter => SelectedSentinel.Current;

    private void Refresh()
    {
        _loadout = GetOrCreateLoadout();
        CharacterSheetCache.Invalidate(ActiveCharacter);
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
        Godot.Collections.Dictionary<CharacterId, EquippedLoadout> loadouts = SaveManager.Instance.Data.Loadouts;

        if (!loadouts.TryGetValue(ActiveCharacter, out EquippedLoadout loadout))
        {
            loadout = new EquippedLoadout();
            loadouts[ActiveCharacter] = loadout;
        }

        return loadout;
    }

    private void RefreshEquipmentSlots()
    {
        SetEquipmentSlotVisual(_weaponButton, "Weapon", SimpleSlot(() => _loadout.Weapon, newItem => _loadout.Weapon = (Weapon)newItem));
        SetEquipmentSlotVisual(_helmetButton, "Helmet", SimpleSlot(() => _loadout.Helmet, newItem => _loadout.Helmet = (Helmet)newItem));
        SetEquipmentSlotVisual(_chestButton, "Chest", SimpleSlot(() => _loadout.Chest, newItem => _loadout.Chest = (Chest)newItem));
        SetEquipmentSlotVisual(_legsButton, "Legs", SimpleSlot(() => _loadout.Legs, newItem => _loadout.Legs = (Legs)newItem));
        SetEquipmentSlotVisual(_bootsButton, "Boots", SimpleSlot(() => _loadout.Boots, newItem => _loadout.Boots = (Boots)newItem));
        SetEquipmentSlotVisual(_glovesButton, "Gloves", SimpleSlot(() => _loadout.Gloves, newItem => _loadout.Gloves = (Gloves)newItem));
        SetEquipmentSlotVisual(_beltButton, "Belt", SimpleSlot(() => _loadout.Belt, newItem => _loadout.Belt = (Belt)newItem));
        SetEquipmentSlotVisual(_amuletButton, "Amulet", SimpleSlot(() => _loadout.Amulet, newItem => _loadout.Amulet = (Amulet)newItem));
        SetEquipmentSlotVisual(_leftRingButton, "Ring", SimpleSlot(() => _loadout.LeftRing, newItem => _loadout.LeftRing = (Ring)newItem));
        SetEquipmentSlotVisual(_rightRingButton, "Ring", SimpleSlot(() => _loadout.RightRing, newItem => _loadout.RightRing = (Ring)newItem));
        SetEquipmentSlotVisual(_relicButton, "Relic", SimpleSlot(() => _loadout.Relic, newItem => _loadout.Relic = (Relic)newItem));
    }

    // Empty slots show their own name as a faint placeholder, and highlight if the currently-held
    // item could go here. Occupied slots show the item's icon tinted by rarity, name hidden - an
    // empty Label still reserves its own row in the VBoxContainer, squeezing the icon otherwise.
    private void SetEquipmentSlotVisual(Button button, string slotName, SlotHandle slot)
    {
        Label label = button.GetNode<Label>("Content/Label");
        Item equippedItem = slot.GetItem();

        button.TooltipText = _heldItem == null && equippedItem != null ? equippedItem.BuildTooltip() : "";

        if (_heldItem != null && slot.CanAccept(_heldItem))
        {
            button.AddThemeStyleboxOverride("normal", CreateBackground(SelectedBg));
        }
        else if (equippedItem != null)
        {
            button.AddThemeStyleboxOverride("normal", CreateBackground(RarityColors.Get(equippedItem.Rarity)));
        }
        else
        {
            button.RemoveThemeStyleboxOverride("normal");
        }

        if (equippedItem != null)
        {
            SetItemVisual(button, equippedItem.Icon, "");
            label.Visible = false;
        }
        else
        {
            SetItemVisual(button, null, slotName);
            label.Visible = true;
            label.Modulate = PlaceholderTint;
        }
    }

    // All 40 slots render every time, empty or not - Inventory's index is the slot, not just a
    // filtered view of "whatever's currently owned and unequipped" like the old flat list was.
    private void RefreshGrid()
    {
        foreach (Node child in Grid.GetChildren())
        {
            child.QueueFree();
        }

        Godot.Collections.Array<Item> inventory = SaveManager.Instance.Data.Inventory;

        for (int slotIndex = 0; slotIndex < inventory.Count; slotIndex++)
        {
            int capturedIndex = slotIndex;
            Item slotItem = inventory[slotIndex];

            Button button = new Button
            {
                CustomMinimumSize = new Vector2(90f, 90f)
            };

            BuildIconOnlyVisual(button);

            if (slotItem != null)
            {
                SetIconOnlyVisual(button, slotItem.Icon);
                Color background = IsCompatible(slotItem) ? RarityColors.Get(slotItem.Rarity) : DisabledBg;
                button.AddThemeStyleboxOverride("normal", CreateBackground(background));
                button.TooltipText = _heldItem == null ? slotItem.BuildTooltip() : "";
            }

            button.Pressed += () => OnSlotPressed(GridSlot(capturedIndex));

            Grid.AddChild(button);
        }
    }

    // The single click-handler for every slot in the panel, grid and equipment alike:
    //  - nothing held + click a slot with an item -> pick it up (hold it, clear the slot).
    //  - holding an item + click a slot that accepts it -> place it there, displacing whatever
    //    was already in that slot back to the earliest open inventory slot.
    //  - holding an item + click a slot that rejects it (wrong type/character) -> do nothing.
    private void OnSlotPressed(SlotHandle slot)
    {
        if (_heldItem != null)
        {
            if (!slot.CanAccept(_heldItem))
            {
                return;
            }

            slot.PlaceItem(_heldItem);
            _heldItem = null;
            SaveManager.Instance.Save();
            Refresh();
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
    // requires the right item subtype and character compatibility, and placing a new item first
    // returns whatever was equipped there back to Inventory before overwriting the field.
    private SlotHandle SimpleSlot<T>(Func<T> getEquipped, Action<Item> assignEquipped) where T : Item
    {
        return new SlotHandle(
            getItem: () => getEquipped(),
            placeItem: newItem =>
            {
                if (newItem != null)
                {
                    ReturnToInventory(getEquipped());
                }

                assignEquipped(newItem);
            },
            canAccept: candidateItem => candidateItem == null || (candidateItem is T && IsCompatible(candidateItem)));
    }

    // Drops a displaced item into the first empty Inventory slot (lowest index first).
    // TODO: if Inventory is completely full, the item is currently just lost. At worst it should
    // fall on the ground instead, but there's no world-drop/pickup functionality yet to do that.
    private static void ReturnToInventory(Item item)
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
        }
    }

    private static StyleBoxFlat CreateBackground(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
    }

    private Button BuildSlotButton(Vector2 anchor)
    {
        Button button = new Button();
        CenterOnAnchor(button, anchor, new Vector2(130f, 130f));
        BuildItemVisual(button);
        SlotArea.AddChild(button);

        return button;
    }

    private static void CenterOnAnchor(Control control, Vector2 anchorPoint, Vector2 size)
    {
        control.AnchorLeft = anchorPoint.X;
        control.AnchorRight = anchorPoint.X;
        control.AnchorTop = anchorPoint.Y;
        control.AnchorBottom = anchorPoint.Y;

        control.OffsetLeft = -size.X / 2f;
        control.OffsetRight = size.X / 2f;
        control.OffsetTop = -size.Y / 2f;
        control.OffsetBottom = size.Y / 2f;
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

    // Grid tiles show only the thumbnail - no name until a tooltip exists to show it on hover.
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
        return item.EquippableCharacter == ActiveCharacter || item.EquippableCharacter == CharacterId.AnySentinel;
    }
}
