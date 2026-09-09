using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SlotHandle = ItemDragController.SlotHandle;

public partial class InventoryPanel : Control
{
    public static InventoryPanel Instance { get; private set; }

    private static readonly Color DisabledBg = new Color(0.32f, 0.14f, 0.14f);
    private static readonly Color AcceptBorderColor = new Color(0.3f, 0.9f, 0.3f);
    private static readonly Color PlaceholderTint = new Color(1f, 1f, 1f, 0.4f);
    private static readonly Color HeldOriginColor = new Color(0.85f, 0.75f, 0.15f);
    private const int AcceptBorderWidth = 3;

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

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        BuildEquipmentSlots();
        BuildGrid();

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
        ItemDragController.Instance.Changed += OnInventoryChangedExternally;
        ItemDragController.Instance.RegisterPanelBounds(this);
        ItemDragController.Instance.RegisterInventoryFallback(item =>
        {
            if (SaveManager.Instance.Data.Inventory.Contains(item))
            {
                return false;
            }

            ReturnToInventory(item);
            return true;
        });
    }

    public override void _ExitTree()
    {
        EventBus.Instance.InventoryChanged -= OnInventoryChangedExternally;
        ItemDragController.Instance.Changed -= OnInventoryChangedExternally;
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
            CreateEquipmentSlot("Legplates", new Vector2(0.50f, 0.72f), SimpleSlot(() => _loadout.Legplates, item => _loadout.Legplates = (Legplates)item)),
            CreateEquipmentSlot("Gloves", new Vector2(0.30f, 0.90f), SimpleSlot(() => _loadout.Gloves, item => _loadout.Gloves = (Gloves)item)),
            CreateEquipmentSlot("Boots", new Vector2(0.70f, 0.90f), SimpleSlot(() => _loadout.Boots, item => _loadout.Boots = (Boots)item))
        };
    }

    // The slot's accessors read _loadout through this panel rather than capturing it, so a handle
    // built once here stays correct after Refresh swaps in a different character's loadout.
    private EquipmentSlotView CreateEquipmentSlot(string placeholderName, Vector2 anchor, SlotHandle slot)
    {
        Button button = BuildSlotButton(anchor);
        button.Pressed += () => OnEquipmentSlotPressed(slot, button);
        button.GuiInput += @event => ItemDragController.Instance.OnSlotGuiInput(slot, button, @event);
        ItemDragController.Instance.RegisterSlot(button, slot);
        ItemTooltip.Instance.Register(button, () => slot.GetItem());

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
            button.Pressed += () => OnGridSlotPressed(slotIndex, button);
            button.GuiInput += @event => ItemDragController.Instance.OnSlotGuiInput(GridSlot(slotIndex), button, @event);
            button.GuiInput += @event => OnGridSlotGuiInput(slotIndex, @event);
            ItemDragController.Instance.RegisterSlot(button, GridSlot(slotIndex));
            ItemTooltip.Instance.Register(button, () => GridSlot(slotIndex).GetItem());

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
        if (PlayerCharacter.ActiveCombatant == null)
        {
            return;
        }

        Visible = true;
        Refresh();
        _equipmentSlots[0].Button.GrabFocus();
        UiPanelStack.Push(this, Close);
    }

    private void Close()
    {
        // Never leave a picked-up item floating outside both the loadout and Inventory.
        Item held = ItemDragController.Instance.TakeHeld();

        if (held != null)
        {
            ReturnToInventory(held);
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
                ClearBackground(button, isHeldOrigin: ItemDragController.Instance.HeldOriginButton == button);
                continue;
            }

            SetBackground(button, IsCompatible(slotItem) ? RarityColors.Get(slotItem.Rarity) : DisabledBg);
        }
    }

    private void OnGridSlotPressed(int gridIndex, Control button)
    {
        if (ItemDragController.Instance.ConsumeDragJustHandled())
        {
            return;
        }

        bool quickEquip = ItemDragController.Instance.Held == null
            && SaveManager.Instance.Data.Inventory[gridIndex] != null
            && Input.IsActionPressed("quick_equip");

        if (quickEquip)
        {
            QuickEquip(gridIndex);
            return;
        }

        ItemDragController.Instance.OnSlotPressed(GridSlot(gridIndex), button);
    }

    // Right-click (inventory_use) a Rune Tome in the grid to learn it immediately, removing it
    // from Inventory - the one item type that isn't meant to be equipped or just sit there.
    private void OnGridSlotGuiInput(int gridIndex, InputEvent @event)
    {
        if (!@event.IsActionPressed("inventory_use"))
        {
            return;
        }

        if (SaveManager.Instance.Data.Inventory[gridIndex] is RuneTome tome)
        {
            LearnRuneTome(gridIndex, tome);
        }
    }

    private void LearnRuneTome(int gridIndex, RuneTome tome)
    {
        if (tome.TaughtRune == null || SaveManager.Instance.Data.KnownRunes.Any(entry => entry.Rune == tome.TaughtRune))
        {
            return;
        }

        LearnRune(tome.TaughtRune);

        foreach (Rune sibling in RuneCatalog.GetRunesOfType(tome.TaughtRune).Where(r => r.Rarity < tome.TaughtRune.Rarity))
        {
            LearnRune(sibling);
        }

        SaveManager.Instance.Data.Inventory[gridIndex] = null;
        CommitChange();
    }

    private static void LearnRune(Rune rune)
    {
        if (SaveManager.Instance.Data.KnownRunes.Any(entry => entry.Rune == rune))
        {
            return;
        }

        SaveManager.Instance.Data.KnownRunes.Add(new RuneInventoryEntry { Rune = rune, Unlimited = true });
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
    public void ReturnToInventory(Item item)
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
