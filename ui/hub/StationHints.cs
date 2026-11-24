using Godot;
using System;
using System.Collections.Generic;

// Hints for a station (stash, vendor, crafting table) open beside Inventory. The station is the top
// panel, so the hint bar only asks it - it speaks for both sides, following whichever holds focus
// (controller) or the cursor (mouse).
public static class StationHints
{
    // carriedLabel replaces the carried-item hint while the cursor is over the station - letting go of
    // an item over the vendor sells it rather than dropping it. Mouse only: a controller has no cursor
    // to let go with.
    public static void Add(List<Hint> hints, Control station, string stationName, string sendLabel,
        Func<Item, bool> canSend, string carriedLabel = null)
    {
        InventoryPanel inventory = InventoryPanel.Instance;
        Control pointed = HintBar.PointedControl(station.GetViewport());
        bool inStation = pointed != null && station.IsAncestorOf(pointed);
        bool inInventory = pointed != null && inventory.IsAncestorOf(pointed);

        if (ItemDragController.Instance.Held != null)
        {
            if (carriedLabel != null && inStation && !InputDevice.UsingController)
            {
                hints.Add(new Hint("ui_accept", carriedLabel, MouseText: "Click"));
            }
            else
            {
                InventoryPanel.AddCarriedHints(hints);
            }

            return;
        }

        Item item = ItemTooltip.Instance.ItemFor(pointed);

        if (inInventory)
        {
            if (item != null && canSend(item))
            {
                hints.Add(QuickMove(sendLabel));
            }

            inventory.AddSlotHints(hints, pointed);
        }
        else if (inStation && item != null)
        {
            // Only a slot can be quick-moved out - a vendor row is bought with a plain press.
            if (ItemDragController.Instance.TryGetSlot(pointed, out _)
                && PlayerProfiles.Local.Data.InventoryGrid.HasRoomFor(item))
            {
                hints.Add(QuickMove("To Inventory"));
            }

            InventoryPanel.AddInspectHints(hints, canCompare: true);
        }

        if (InputDevice.UsingController)
        {
            hints.Add(inStation ? new Hint("token_modifier", "Inventory") : new Hint("ability_modifier", stationName));
        }
    }

    private static Hint QuickMove(string label) => new Hint("quick_move", label, MouseText: "{0} + Click");
}
