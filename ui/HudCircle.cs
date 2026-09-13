using Godot;

// Shared visual building blocks for the field HUD's circular slot rows (TokenBar, AbilityBar) -
// both are a strip of same-sized circles with a numbered badge at the bottom edge, differing only
// in what fills them, so the node construction lives in one place instead of two copies.
public static class HudCircle
{
    public const float CircleSize = 83f;
    public const float Spacing = 16f;
    public const float BadgeSize = 29f;
    public const float RowGap = BadgeSize;

    private static readonly Color _badgeColor = new Color(0f, 0f, 0f, 0.85f);

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

    public static Panel CreateBadge(Panel circle, int number)
    {
        Panel badge = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        badge.Size = new Vector2(BadgeSize, BadgeSize);
        badge.Position = new Vector2(CircleSize / 2f - BadgeSize / 2f, CircleSize - BadgeSize / 2f);
        badge.AddThemeStyleboxOverride("panel", BuildCircularStyle(_badgeColor));
        circle.AddChild(badge);

        Label numberLabel = new Label
        {
            Text = number.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        numberLabel.AddThemeFontSizeOverride("font_size", 13);
        numberLabel.AddThemeColorOverride("font_color", Colors.White);
        numberLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        badge.AddChild(numberLabel);

        return badge;
    }

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

    public static float RowWidth(int slotCount) => slotCount * CircleSize + (slotCount - 1) * Spacing;

    // A row's own full extent, including the badge that hangs BadgeSize/2 below the circle.
    public static float RowHeight => CircleSize + BadgeSize / 2f;
}
