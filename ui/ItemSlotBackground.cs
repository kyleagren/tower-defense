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

    // Rarity fill plus a colored border on top - marks a specific state (e.g. "currently socketed
    // here") without losing the rarity color underneath, unlike swapping to a full
    // ThemeTypeVariation, whose own styles a local stylebox override like Set's always shadows.
    public static void SetBorder(Button button, Color fillColor, Color borderColor)
    {
        StyleBoxFlat style = BuildStyle(fillColor);
        style.BorderColor = borderColor;
        style.SetBorderWidthAll(3);
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
    }

    // Flat black, explicitly covering the "disabled" style too rather than relying on Godot's
    // fallback - a tile the player categorically cannot pick right now doesn't need its rarity
    // legible, it needs to read as unavailable at a glance.
    public static void SetUnavailable(Button button)
    {
        StyleBoxFlat style = BuildStyle(Colors.Black);
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("disabled", style);
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
