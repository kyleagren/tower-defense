using Godot;

// Shared visual building blocks for the field HUD's circular slot rows (TokenBar, AbilityBar) -
// both are a strip of same-sized circles with a button-prompt badge at the bottom edge, differing
// only in what fills them, so the node construction lives in one place instead of two copies.
public static class HudCircle
{
    public const float CircleSize = 83f;
    public const float Spacing = 16f;
    public const float BadgeSize = 29f;
    public const float RowGap = BadgeSize;
    public const float ChipSize = 32f;
    // Room at the left of each row for its modifier chip (L1, R1, or Q).
    public const float ChipWidth = ChipSize + 8f;

    public static Panel CreateCircle(Control parent, Vector2 position)
    {
        Panel circle = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        circle.Position = position;
        circle.Size = new Vector2(CircleSize, CircleSize);
        parent.AddChild(circle);
        return circle;
    }

    public static CooldownOverlay CreateCooldownOverlay(Panel circle)
    {
        CooldownOverlay overlay = new CooldownOverlay { MouseFilter = Control.MouseFilterEnum.Ignore };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        circle.AddChild(overlay);
        return overlay;
    }

    public static PromptBadge CreateBadge(Panel circle) =>
        PromptBadge.Create(circle, new Vector2(CircleSize / 2f - BadgeSize / 2f, CircleSize - BadgeSize / 2f), BadgeSize);

    public static PromptBadge CreateChip(Control row) =>
        PromptBadge.Create(row, new Vector2(0f, CircleSize / 2f - ChipSize / 2f), ChipSize);

    public static float SlotX(int index) => ChipWidth + index * (CircleSize + Spacing);

    public static StyleBoxFlat BuildCircularStyle(Color fillColor, Color? borderColor = null)
    {
        StyleBoxFlat style = new StyleBoxFlat
        {
            BgColor = fillColor,
            CornerRadiusTopLeft = 999,
            CornerRadiusTopRight = 999,
            CornerRadiusBottomLeft = 999,
            CornerRadiusBottomRight = 999
        };

        if (borderColor.HasValue)
        {
            style.BorderColor = borderColor.Value;
            style.SetBorderWidthAll(3);
        }

        return style;
    }

    public static float RowWidth(int slotCount) => ChipWidth + slotCount * CircleSize + (slotCount - 1) * Spacing;

    // A row's own full extent, including the badge that hangs BadgeSize/2 below the circle.
    public static float RowHeight => CircleSize + BadgeSize / 2f;
}
