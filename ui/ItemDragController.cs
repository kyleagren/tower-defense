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

    private readonly Dictionary<Control, SlotHandle> _slotsByControl = new();
    private readonly List<Control> _panelBounds = new();

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
    // change" pattern InventoryPanel already used before this was extracted.
    public void RegisterSlot(Control button, SlotHandle slot)
    {
        _slotsByControl[button] = slot;
    }

    // Registers a panel's root Control so a drag release inside its bounds - but not on any slot -
    // snaps back to source instead of being treated the same as a release out in the 3D world.
    public void RegisterPanelBounds(Control panel)
    {
        _panelBounds.Add(panel);
    }

    // The single click-handler for every registered slot, across every panel:
    //  - nothing held + click a slot with an item -> pick it up (hold it, clear the slot).
    //  - holding an item + click a slot that accepts it -> place it there; whatever was already
    //    in that slot (if anything) becomes the newly held item, so it's a swap, not a loss.
    //  - holding an item + click a slot that rejects it (wrong type/character) -> do nothing.
    public void OnSlotPressed(SlotHandle slot)
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
            CommitChange();
            return;
        }

        Item pickedUpItem = slot.GetItem();

        if (pickedUpItem == null)
        {
            return;
        }

        SetHeld(pickedUpItem);
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
            SetHeld(displaced);
            CommitChange();
            return;
        }

        if (IsInsideAnyOpenPanel(GetViewport().GetMousePosition()))
        {
            ReturnHeldToSource();
            return;
        }

        DropHeldInWorld();
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
    }
}
