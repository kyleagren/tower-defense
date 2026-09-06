using Godot;

// Builds the display for a single rolled mod - description (with its rolled value's range
// inline) on the left, its tier as a small badge on the right. Shared by the crafting table's
// bars and ItemTooltip's advanced (Alt-hover) view, so one mod line renders identically in both
// places rather than two near-duplicate layouts. TierBadge is exposed separately so ItemTooltip
// can wire its own hover-for-details popup on just that element - CraftingPanel's bars ignore it.
public readonly struct AffixRow
{
    public readonly Control Root;
    public readonly Control TierBadge;

    public AffixRow(Control root, Control tierBadge)
    {
        Root = root;
        TierBadge = tierBadge;
    }
}

public static class AffixRowView
{
    private static readonly Color TierColor = new Color(0.65f, 0.65f, 0.65f);

    public static AffixRow Build(RolledAffix roll)
    {
        HBoxContainer row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };

        Label description = new Label
        {
            Text = roll.BuildInlineRangeDescription(),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(description);

        Label tier = new Label { Text = $"T{roll.Tier}", MouseFilter = Control.MouseFilterEnum.Stop };
        tier.AddThemeColorOverride("font_color", TierColor);
        row.AddChild(tier);

        return new AffixRow(row, tier);
    }
}
