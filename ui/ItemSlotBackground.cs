using Godot;

// The rounded, rarity-colored button background used to mark an item slot's rarity at a glance -
// shared by the Vendor and Crafting table so the same visual language means the same thing
// everywhere an item sits in a Button. InventoryPanel keeps its own richer version (it also draws
// a drag-accept border), so it isn't switched over to this one.
public static class ItemSlotBackground
{
    public static void Set(Button button, Color color)
    {
        button.AddThemeStyleboxOverride("normal", BuildStyle(color));
        button.AddThemeStyleboxOverride("hover", BuildStyle(color));
    }

    public static void Clear(Button button)
    {
        button.RemoveThemeStyleboxOverride("normal");
        button.RemoveThemeStyleboxOverride("hover");
    }

    private static StyleBoxFlat BuildStyle(Color color)
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
}
