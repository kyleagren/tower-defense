using Godot;
using System;
using System.Collections.Generic;

// Shared "currently being dragged/held by the mouse" item state, plus the single cursor-icon
// overlay that follows it - extracted from InventoryPanel so a second panel (the crafting table)
// can pick an item up in one and drop it in the other. A CanvasLayer (like Crosshair) so the
// cursor icon always draws above whichever panel is topmost, regardless of tree order - the same
// layering quirk that made the crosshair show through menus before that got its own fix.
public partial class ItemDragController : CanvasLayer
{
    // A stand-in for "a place an Item can sit" - shared across every panel that wants to
    // participate in dragging (InventoryPanel's 40 grid + 11 equipment slots, the crafting
    // table's single slot, and any future one).
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
    private const string WorldItemScenePath = "res://world_items/WorldItem.tscn";
    private const float DropDistance = 1f;
    private const float DropDistanceVariance = 0.2f;
    private const float DropAngleSpread = 45f;

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

        _cursorIcon.GlobalPosition = GetViewport().GetMousePosition() - _cursorIcon.Size / 2f;
    }

    // Registers a button for its whole lifetime - the same "buttons live forever, only contents
    // change" pattern InventoryPanel already used before this was extracted. Sweeps out any dead
    // keys first: this controller is an autoload and outlives every panel, so without this,
    // leaving and re-entering the hub would pile a fresh registration on top of the previous
    // visit's stale one on every visit instead of replacing it.
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
        slot.PlaceItem(null);
        Changed?.Invoke();
    }

    // Detects a press-drag-release gesture layered on top of the existing click-click flow. A
    // plain click that never drifts past DragThreshold touches none of this - it falls straight
    // through to Pressed exactly as before, which is why gamepad confirm (which never emits these
    // event types) can't be affected by any of it.
    public void OnSlotGuiInput(SlotHandle slot, Control button, InputEvent @event)
    {
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
        SaveManager.Instance.RequestSave();
        Changed?.Invoke();

        if (item == null)
        {
            return;
        }

        PlayerCharacter player = PlayerCharacter.ActiveCombatant;
        Vector3 forward = -player.GlobalTransform.Basis.Z;
        Vector3 direction = forward.Rotated(Vector3.Up, Mathf.DegToRad((float)GD.RandRange(-DropAngleSpread, DropAngleSpread)));
        float distance = DropDistance + (float)GD.RandRange(-DropDistanceVariance, DropDistanceVariance);

        WorldItem worldItem = GD.Load<PackedScene>(WorldItemScenePath).Instantiate<WorldItem>();
        worldItem.Initialize(item);
        GetTree().CurrentScene.AddChild(worldItem);
        worldItem.GlobalPosition = player.GlobalPosition + direction * distance;
    }

    // Every mutating placement ends the same way: persist, tell the rest of the game a loadout
    // might have changed (which is what drops the CharacterSheet cache and every Ritual's cached
    // context), then let every open panel redraw.
    private void CommitChange()
    {
        SaveManager.Instance.RequestSave();
        EventBus.Instance.EmitSignal(EventBus.SignalName.InventoryChanged);
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
        }
    }
}
