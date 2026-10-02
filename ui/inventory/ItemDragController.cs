using Godot;
using System;
using System.Collections.Generic;

// Shared held-item state plus the cursor icon that follows it. Any panel registers its slots here
// to participate, so an item can be picked up in one panel and dropped in another. A CanvasLayer so
// the icon draws above every panel regardless of tree order.
public partial class ItemDragController : CanvasLayer
{
    // "A place an Item can sit," implemented per slot by each panel that registers one.
    public readonly struct SlotHandle
    {
        public readonly Func<Item> GetItem;

        // Puts an item here - null takes out what's here - and returns whatever that displaced.
        public readonly Func<Item, Item> PlaceItem;

        // Whether item can go here, displacing at most one item.
        public readonly Func<Item, bool> CanAccept;

        // What placing item here would displace, or null for open space.
        public readonly Func<Item, Item> WouldDisplace;

        // Called just before this slot's item is lifted, for the slot that puts it back exactly where it
        // was. Null when that's this slot itself; a grid cell isn't, since it centers an item on itself.
        public readonly Func<SlotHandle> ReturnSlot;

        public SlotHandle(Func<Item> getItem, Func<Item, Item> placeItem, Func<Item, bool> canAccept,
            Func<Item, Item> wouldDisplace, Func<SlotHandle> returnSlot)
        {
            GetItem = getItem;
            PlaceItem = placeItem;
            CanAccept = canAccept;
            WouldDisplace = wouldDisplace;
            ReturnSlot = returnSlot;
        }

        // A slot holding one item at a time - an equipment slot, the crafting table.
        public static SlotHandle Single(Func<Item> getItem, Action<Item> setItem, Func<Item, bool> canAccept) => new SlotHandle(
            getItem,
            placeItem: item =>
            {
                Item displaced = getItem();
                setItem(item);
                return displaced;
            },
            canAccept,
            wouldDisplace: _ => getItem(),
            returnSlot: null);

        public SlotHandle ReturnTarget() => ReturnSlot?.Invoke() ?? this;
    }

    private const float DragThreshold = 8f;
    private const float HeldIconCellSize = 48f;
    private const float DropHoldDuration = 0.5f;

    public static ItemDragController Instance { get; private set; }

    // Fires after the held item or any registered slot's contents change - every open panel
    // refreshes off this instead of the controller reaching into panel-specific refresh methods.
    public event Action Changed;

    public Item Held { get; private set; }
    public Control HeldOriginButton { get; private set; }

    private readonly Dictionary<Control, SlotHandle> _slotsByControl = new();
    private readonly List<Control> _panelBounds = new();
    private readonly List<(GodotObject Owner, Func<bool> IsOpen, Func<Item, bool> TryAccept)> _quickMoveTargets = new();
    private readonly List<(Control Bounds, Action<Item> OnSell)> _sellTargets = new();
    private Func<Item, bool> _inventoryFallback;

    private TextureRect _cursorIcon;

    private SlotHandle? _dragSourceSlot;
    private Control _dragOriginButton;
    private Vector2 _dragStartPosition;
    private bool _isDragging;
    private bool _dragJustHandled;

    private SlotHandle? _heldSource;
    private bool _cancelHeld;
    private float _cancelHeldTime;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        _cursorIcon = new TextureRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Visible = false
        };
        AddChild(_cursorIcon);
    }

    public override void _Process(double delta)
    {
        if (Held == null)
        {
            return;
        }

        _cursorIcon.GlobalPosition = HeldIconAnchor() - _cursorIcon.Size / 2f;

        if (_cancelHeld)
        {
            _cancelHeldTime += (float)delta;

            if (_cancelHeldTime >= DropHoldDuration)
            {
                _cancelHeld = false;
                DropHeldInWorld();
            }
        }
    }

    // While carrying an item on a controller, Circle stops meaning "close the panel": a tap puts
    // the item back, a hold drops it on the ground. _ShortcutInput runs after every _Input and
    // before any _UnhandledInput, so it claims the press after InputDevice has seen it but before
    // a panel's own ui_cancel handling closes everything.
    public override void _ShortcutInput(InputEvent @event)
    {
        if (@event is not InputEventJoypadButton)
        {
            return;
        }

        if (@event.IsActionPressed("ui_cancel") && Held != null && UiPanelStack.AnyOpen)
        {
            _cancelHeld = true;
            _cancelHeldTime = 0f;
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionReleased("ui_cancel") && _cancelHeld)
        {
            _cancelHeld = false;
            PutHeldBack();
            GetViewport().SetInputAsHandled();
        }
    }

    // Back to the slot it came from if that's still free, else the first open Inventory slot,
    // else the ground - never left floating.
    private void PutHeldBack()
    {
        if (Held == null)
        {
            return;
        }

        if (_heldSource is SlotHandle source && source.CanAccept(Held) && source.WouldDisplace(Held) == null)
        {
            source.PlaceItem(Held);
            SetHeld(null);
            CommitChange();
            return;
        }

        if (_inventoryFallback != null && _inventoryFallback(Held))
        {
            SetHeld(null);
            CommitChange();
            return;
        }

        DropHeldInWorld();
    }

    // A controller has no pointer, so the held item rides on the focused slot's corner instead.
    private Vector2 HeldIconAnchor()
    {
        if (InputDevice.UsingController && GetViewport().GuiGetFocusOwner() is Control focused)
        {
            Rect2 rect = focused.GetGlobalRect();
            return rect.Position + new Vector2(rect.Size.X, 0f);
        }

        return GetViewport().GetMousePosition();
    }

    // Registers a button for its whole lifetime; only its contents change. Dead keys are swept
    // first because this controller is an autoload that outlives every panel - a panel freed on
    // leaving the hub would otherwise leave a disposed registration behind on every visit.
    public void RegisterSlot(Control button, SlotHandle slot)
    {
        PruneDeadSlots();
        _slotsByControl[button] = slot;
    }

    private void PruneDeadSlots()
    {
        List<Control> deadKeys = new();

        foreach (Control key in _slotsByControl.Keys)
        {
            if (!IsInstanceValid(key))
            {
                deadKeys.Add(key);
            }
        }

        foreach (Control key in deadKeys)
        {
            _slotsByControl.Remove(key);
        }
    }

    // Registers a panel's root Control so a drag release inside its bounds - but not on any slot -
    // snaps back to source instead of being treated the same as a release out in the 3D world.
    public void RegisterPanelBounds(Control panel)
    {
        _panelBounds.Add(panel);
    }

    // Registers a Ctrl+click destination for "move this item somewhere else useful right now" -
    // tried in registration order the moment Ctrl+click picks up an item, so whichever panel is
    // open and can take it gets first claim. Both Crafting's single slot and the vendor's
    // always-open sell zone register here. owner is used only to detect a stale registration left
    // behind by a panel that's since been freed (see TryQuickMove) - pass the registering panel.
    public void RegisterQuickMoveTarget(GodotObject owner, Func<bool> isOpen, Func<Item, bool> tryAccept)
    {
        _quickMoveTargets.Add((owner, isOpen, tryAccept));
    }

    // Registers a zone (typically a whole panel) where dropping a held item anywhere inside counts
    // as selling it, rather than the usual "not a slot -> bounce back to source" rule.
    public void RegisterSellTarget(Control bounds, Action<Item> onSell)
    {
        _sellTargets.Add((bounds, onSell));
    }

    // The one universal "nothing else wanted it -> send it home" destination, always tried last
    // regardless of registration order - unlike the ordered, competing _quickMoveTargets list,
    // there's exactly one Inventory, so it doesn't need a place in line. Registered by
    // InventoryPanel itself, which knows how to reject an item that's already sitting in it (so a
    // plain Ctrl+click on a grid item, with nothing else open, doesn't get silently reshuffled).
    public void RegisterInventoryFallback(Func<Item, bool> tryAccept)
    {
        _inventoryFallback = tryAccept;
    }

    // The single click-handler for every registered slot, across every panel:
    //  - nothing held + click a slot with an item -> pick it up (hold it, clear the slot).
    //  - holding an item + click a slot that accepts it -> place it there; whatever was already
    //    in that slot (if anything) becomes the newly held item, so it's a swap, not a loss.
    //  - holding an item + click a slot that rejects it (wrong type/character) -> do nothing.
    public void OnSlotPressed(SlotHandle slot, Control button)
    {
        if (Held != null)
        {
            if (!slot.CanAccept(Held))
            {
                return;
            }

            SetHeld(slot.PlaceItem(Held));
            HeldOriginButton = null;
            _heldSource = null;
            CommitChange();
            return;
        }

        if (slot.GetItem() == null)
        {
            return;
        }

        SlotHandle source = slot.ReturnTarget();
        SetHeld(slot.PlaceItem(null));
        HeldOriginButton = button;
        _heldSource = source;
        Changed?.Invoke();
    }

    // Press-drag-release, layered over the click-to-pick/click-to-place flow. A click that never
    // drifts past DragThreshold touches none of this and falls through to Pressed, which is what
    // keeps gamepad confirm working - it never emits these event types at all.
    public void OnSlotGuiInput(SlotHandle slot, Control button, InputEvent @event)
    {
        // A controller has no Ctrl+click, so quick move gets its own button press.
        if (@event is InputEventJoypadButton && @event.IsActionPressed("quick_move"))
        {
            if (Held == null && slot.GetItem() != null)
            {
                TryQuickMove(slot);
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                if (Held == null && slot.GetItem() != null)
                {
                    if (Input.IsActionPressed("quick_move") && TryQuickMove(slot))
                    {
                        return;
                    }

                    _dragSourceSlot = slot;
                    _dragOriginButton = button;
                    _dragStartPosition = mouseButton.Position;
                    _isDragging = false;
                }

                return;
            }

            try
            {
                if (_isDragging)
                {
                    EndDrag();
                }
            }
            finally
            {
                _dragSourceSlot = null;
                _dragOriginButton = null;
            }

            return;
        }

        if (@event is InputEventMouseMotion mouseMotion && _dragSourceSlot != null && !_isDragging
            && mouseMotion.Position.DistanceTo(_dragStartPosition) >= DragThreshold)
        {
            BeginDrag();
        }
    }

    // True for exactly one call right after a drag completes - lets a panel's own Button.Pressed
    // handler (which still fires for that same release) tell "a real click" apart from "the
    // release that just ended a drag," without needing to know about drag internals.
    public bool ConsumeDragJustHandled()
    {
        if (!_dragJustHandled)
        {
            return false;
        }

        _dragJustHandled = false;
        return true;
    }

    // Ctrl+click a slot with an item: offers it to each registered quick-move target in turn, and
    // the first one that's open and accepts it wins. Falls through to a normal click/drag if no
    // target wants it right now.
    private bool TryQuickMove(SlotHandle sourceSlot)
    {
        _quickMoveTargets.RemoveAll(target => !IsInstanceValid(target.Owner));

        Item item = sourceSlot.GetItem();

        foreach (var (_, isOpen, tryAccept) in _quickMoveTargets)
        {
            if (!isOpen() || !tryAccept(item))
            {
                continue;
            }

            sourceSlot.PlaceItem(null);
            CommitChange();
            return true;
        }

        if (_inventoryFallback != null && _inventoryFallback(item))
        {
            sourceSlot.PlaceItem(null);
            CommitChange();
            return true;
        }

        return false;
    }

    // Never leave a picked-up item floating outside every slot - a panel calls this when it closes
    // while still holding something, then decides where the returned item goes (its own concept
    // of "natural home," e.g. InventoryPanel returns it to the grid).
    public Item TakeHeld()
    {
        Item item = Held;
        SetHeld(null);

        if (item != null)
        {
            Changed?.Invoke();
        }

        return item;
    }

    // Lifts the item the moment the mouse actually moves, rather than waiting for release - the
    // shared cursor icon (already driven by Held) starts following the drag immediately.
    private void BeginDrag()
    {
        SlotHandle source = _dragSourceSlot.Value.ReturnTarget();
        SetHeld(_dragSourceSlot.Value.PlaceItem(null));
        HeldOriginButton = _dragOriginButton;
        _heldSource = source;
        _isDragging = true;
        Changed?.Invoke();
    }

    // Finishes a drag: onto another slot that accepts it (swapping with whatever was there), into a
    // sell zone, back to its own slot, snapped back if released elsewhere inside a panel, or dropped
    // in the world if released outside every panel.
    private void EndDrag()
    {
        _isDragging = false;
        _dragJustHandled = true;
        CallDeferred(nameof(ClearDragJustHandled));

        Control hovered = GetViewport().GuiGetHoveredControl();

        // Back over the slot it started from: straight back in.
        if (hovered == _dragOriginButton)
        {
            PutHeldBack();
            return;
        }

        if (hovered != null && _slotsByControl.TryGetValue(hovered, out SlotHandle targetSlot) && targetSlot.CanAccept(Held))
        {
            SlotHandle? origin = _heldSource;
            Item displaced = targetSlot.PlaceItem(Held);

            if (displaced != null && origin is SlotHandle source && source.CanAccept(displaced) && source.WouldDisplace(displaced) == null)
            {
                source.PlaceItem(displaced);
                SetHeld(null);
            }
            else
            {
                SetHeld(displaced);
                HeldOriginButton = null;
                _heldSource = null;
            }

            CommitChange();
            return;
        }

        Vector2 mousePosition = GetViewport().GetMousePosition();

        if (TryDropOnSellTarget(mousePosition))
        {
            return;
        }

        // Released inside a panel but not on a slot that takes it: back where it came from.
        if (IsInsideAnyOpenPanel(mousePosition))
        {
            PutHeldBack();
            return;
        }

        DropHeldInWorld();
    }

    // Dropped anywhere inside a registered sell zone (not necessarily on any particular control
    // within it) - sells the held item instead of the usual bounce-back-if-not-a-slot rule.
    private bool TryDropOnSellTarget(Vector2 mousePosition)
    {
        _sellTargets.RemoveAll(target => !IsInstanceValid(target.Bounds));

        foreach (var (bounds, onSell) in _sellTargets)
        {
            if (!bounds.Visible || !bounds.GetGlobalRect().HasPoint(mousePosition))
            {
                continue;
            }

            Item item = Held;
            SetHeld(null);
            onSell(item);
            CommitChange();
            return true;
        }

        return false;
    }

    private bool IsInsideAnyOpenPanel(Vector2 mousePosition)
    {
        _panelBounds.RemoveAll(panel => !IsInstanceValid(panel));

        foreach (Control panel in _panelBounds)
        {
            if (panel.Visible && panel.GetGlobalRect().HasPoint(mousePosition))
            {
                return true;
            }
        }

        return false;
    }

    private void ClearDragJustHandled()
    {
        _dragJustHandled = false;
    }

    private void DropHeldInWorld()
    {
        Item item = Held;
        SetHeld(null);
        PlayerProfiles.Local.CommitChange();
        Changed?.Invoke();

        if (item != null)
        {
            LevelContext.Current.WorldItemRoster.DropNearLocalPlayer(item);
        }
    }

    // Every mutating placement ends the same way: the profile persists it and tells the rest of the
    // game, then every open panel redraws.
    private void CommitChange()
    {
        PlayerProfiles.Local.CommitChange();
        Changed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Held != null && UiPanelStack.AnyOpen
            && @event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            DropHeldInWorld();
            GetViewport().SetInputAsHandled();
        }
    }

    // The carried icon takes the item's grid shape, so a weapon reads as tall and a ring as small.
    private void SetHeld(Item item)
    {
        Held = item;
        _cursorIcon.Visible = Held != null;
        _cursorIcon.Texture = Held?.Icon;

        if (item != null)
        {
            _cursorIcon.Size = new Vector2(item.GridSize.X, item.GridSize.Y) * HeldIconCellSize;
        }
        else
        {
            HeldOriginButton = null;
            _heldSource = null;
        }
    }
}
