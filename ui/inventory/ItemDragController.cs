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
        public readonly Action<Item> PlaceItem;
        public readonly Func<Item, bool> CanAccept;

        public SlotHandle(Func<Item> getItem, Action<Item> placeItem, Func<Item, bool> canAccept)
        {
            GetItem = getItem;
            PlaceItem = placeItem;
            CanAccept = canAccept;
        }
    }

    private const float DragThreshold = 8f;
    // Stays a res:// path rather than an [Export] PackedScene: this class is registered directly
    // as a bare-script autoload (project.godot), with no .tscn wrapper and so no Inspector slot to
    // wire one into. world_item.tscn also has no uid assigned yet (never resaved by the editor), so
    // there's no move-proof alternative available for it either.
    private const string WorldItemScenePath = "res://world_items/world_item.tscn";
    private const float DropDistance = 1f;
    private const float DropDistanceVariance = 0.2f;
    private const float DropAngleSpread = 45f;
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
            Size = new Vector2(72f, 72f),
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

        if (_heldSource is SlotHandle source && source.GetItem() == null && source.CanAccept(Held))
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

            Item displaced = slot.GetItem();
            slot.PlaceItem(Held);
            SetHeld(displaced);
            HeldOriginButton = null;
            _heldSource = null;
            CommitChange();
            return;
        }

        Item pickedUpItem = slot.GetItem();

        if (pickedUpItem == null)
        {
            return;
        }

        SetHeld(pickedUpItem);
        HeldOriginButton = button;
        _heldSource = slot;
        slot.PlaceItem(null);
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
        SetHeld(_dragSourceSlot.Value.GetItem());
        HeldOriginButton = _dragOriginButton;
        _dragSourceSlot.Value.PlaceItem(null);
        _isDragging = true;
        Changed?.Invoke();
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

        if (hovered != null && _slotsByControl.TryGetValue(hovered, out SlotHandle targetSlot) && targetSlot.CanAccept(Held))
        {
            Item displaced = targetSlot.GetItem();
            targetSlot.PlaceItem(Held);

            if (displaced != null && _dragSourceSlot.HasValue && _dragSourceSlot.Value.CanAccept(displaced))
            {
                _dragSourceSlot.Value.PlaceItem(displaced);
                SetHeld(null);
            }
            else
            {
                SetHeld(displaced);
                HeldOriginButton = null;
            }

            CommitChange();
            return;
        }

        Vector2 mousePosition = GetViewport().GetMousePosition();

        if (TryDropOnSellTarget(mousePosition))
        {
            return;
        }

        if (IsInsideAnyOpenPanel(mousePosition))
        {
            ReturnHeldToSource();
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

    // Released somewhere that can't take the item - a slot that rejects it, the gap between grid
    // squares, a margin, anything inside a panel that isn't itself a slot - snap back to wherever
    // it was picked up from rather than losing it.
    private void ReturnHeldToSource()
    {
        _dragSourceSlot.Value.PlaceItem(Held);
        SetHeld(null);
        Changed?.Invoke();
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

        if (item == null)
        {
            return;
        }

        PlayerCharacter player = LevelContext.Current?.LocalActiveCharacter;
        Vector3 forward = -player.GlobalTransform.Basis.Z;
        Vector3 direction = forward.Rotated(Vector3.Up, Mathf.DegToRad((float)GD.RandRange(-DropAngleSpread, DropAngleSpread)));
        float distance = DropDistance + (float)GD.RandRange(-DropDistanceVariance, DropDistanceVariance);

        WorldItem worldItem = GD.Load<PackedScene>(WorldItemScenePath).Instantiate<WorldItem>();
        worldItem.Initialize(item);
        LevelContext.Current.LevelRoot.AddChild(worldItem);
        worldItem.GlobalPosition = player.GlobalPosition + direction * distance;
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

    private void SetHeld(Item item)
    {
        Held = item;
        _cursorIcon.Visible = Held != null;
        _cursorIcon.Texture = Held?.Icon;

        if (item == null)
        {
            HeldOriginButton = null;
            _heldSource = null;
        }
    }
}
