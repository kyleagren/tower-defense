using Godot;

// Field HUD readout: a permanent 3-slot token row (README section 17) stacked above a 4-slot
// ability row (all empty placeholders for now - no ability-kit system exists yet, README section
// 10). Tokens are always shown, filled/empty and colored by spell, so their status reads at a
// glance without needing to enter pick mode. The two rows share the 1/2/3 keys, so only one shows
// number badges at a time - abilities normally, tokens for as long as pick mode (Q) is active -
// rather than implying a key does two different things simultaneously.
public partial class TokenBar : Control
{
    private const int AbilitySlotCount = 4;
    private const float CircleSize = 83f;
    private const float Spacing = 16f;
    private const float BadgeSize = 29f;
    private const float RowGap = BadgeSize;

    private static readonly Color _emptyColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
    private static readonly Color _pickModeBorder = new Color(0.85f, 0.75f, 0.15f);
    private static readonly Color _badgeColor = new Color(0f, 0f, 0f, 0.85f);

    private readonly Panel[] _tokenCircles = new Panel[GameRules.MaxTokens];
    private readonly Label[] _tokenNameLabels = new Label[GameRules.MaxTokens];
    private readonly Panel[] _tokenBadges = new Panel[GameRules.MaxTokens];
    private readonly Panel[] _abilityBadges = new Panel[AbilitySlotCount];

    private bool _pickModeActive;

    public override void _Ready()
    {
        float width = AbilitySlotCount * CircleSize + (AbilitySlotCount - 1) * Spacing;
        float height = CircleSize * 2f + RowGap + BadgeSize / 2f;
        CustomMinimumSize = new Vector2(width, height);

        for (int i = 0; i < GameRules.MaxTokens; i++)
        {
            Panel circle = CreateCircle(new Vector2(i * (CircleSize + Spacing), 0f));
            _tokenCircles[i] = circle;

            Label nameLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 11);
            // Elemental fills are all fairly bright - black reads better on them than the theme's
            // default light text color.
            nameLabel.AddThemeColorOverride("font_color", Colors.Black);
            nameLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            circle.AddChild(nameLabel);
            _tokenNameLabels[i] = nameLabel;

            _tokenBadges[i] = CreateBadge(circle, i + 1);
        }

        // Abilities have no dynamic state yet (no ability-kit system exists), so their fill is set
        // once here rather than recomputed every Refresh like the token row's is.
        float abilityRowY = CircleSize + RowGap;

        for (int i = 0; i < AbilitySlotCount; i++)
        {
            Panel circle = CreateCircle(new Vector2(i * (CircleSize + Spacing), abilityRowY));
            circle.AddThemeStyleboxOverride("panel", BuildCircularStyle(_emptyColor));

            _abilityBadges[i] = CreateBadge(circle, i + 1);
        }

        EventBus.Instance.TokensChanged += Refresh;
        EventBus.Instance.TokenPickModeChanged += OnPickModeChanged;
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        Refresh();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.TokensChanged -= Refresh;
        EventBus.Instance.TokenPickModeChanged -= OnPickModeChanged;
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
    }

    private void OnGamePhaseChanged(GamePhase phase) => Refresh();

    private void OnPickModeChanged(bool active)
    {
        _pickModeActive = active;
        Refresh();
    }

    private void Refresh()
    {
        Godot.Collections.Array<RuneLoadout> tokens = SaveManager.Instance.Data.Tokens;

        for (int i = 0; i < GameRules.MaxTokens; i++)
        {
            RuneLoadout token = i < tokens.Count ? tokens[i] : null;

            Color fill = token != null ? DamageTypeColors.GetForSpell(token.Spell) : _emptyColor;
            Color? border = token != null && _pickModeActive ? _pickModeBorder : (Color?)null;

            _tokenCircles[i].AddThemeStyleboxOverride("panel", BuildCircularStyle(fill, border));
            _tokenNameLabels[i].Text = token != null
                ? (string.IsNullOrEmpty(token.Name) ? token.Spell?.Name ?? "" : token.Name)
                : "";

            _tokenBadges[i].Visible = _pickModeActive;
        }

        for (int i = 0; i < AbilitySlotCount; i++)
        {
            _abilityBadges[i].Visible = !_pickModeActive;
        }
    }

    private Panel CreateCircle(Vector2 position)
    {
        Panel circle = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        circle.Position = position;
        circle.Size = new Vector2(CircleSize, CircleSize);
        AddChild(circle);
        return circle;
    }

    private Panel CreateBadge(Panel circle, int number)
    {
        Panel badge = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        badge.Size = new Vector2(BadgeSize, BadgeSize);
        badge.Position = new Vector2(CircleSize / 2f - BadgeSize / 2f, CircleSize - BadgeSize / 2f);
        badge.AddThemeStyleboxOverride("panel", BuildCircularStyle(_badgeColor));
        circle.AddChild(badge);

        Label numberLabel = new Label
        {
            Text = number.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        numberLabel.AddThemeFontSizeOverride("font_size", 13);
        numberLabel.AddThemeColorOverride("font_color", Colors.White);
        numberLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        badge.AddChild(numberLabel);

        return badge;
    }

    private static StyleBoxFlat BuildCircularStyle(Color fillColor, Color? borderColor = null)
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
}
