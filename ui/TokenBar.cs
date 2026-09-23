using Godot;

// The field HUD's 3-slot token row. Filled/empty state and spell color always show, so status
// reads at a glance without entering pick mode; number badges only show while pick mode (Q) is
// active, since 1/2/3 map to tokens only then. AbilityBar (its sibling under AbilityHud) shows its
// own badges the rest of the time - each row decides independently off the same
// TokenPickModeChanged signal, with no shared state between them.
public partial class TokenBar : Control
{
    private static readonly Color _emptyColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
    private static readonly Color _pickModeBorder = new Color(0.85f, 0.75f, 0.15f);

    private readonly Panel[] _tokenCircles = new Panel[GameRules.MaxTokens];
    private readonly Label[] _tokenNameLabels = new Label[GameRules.MaxTokens];
    private readonly Panel[] _tokenBadges = new Panel[GameRules.MaxTokens];

    private bool _pickModeActive;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(HudCircle.RowWidth(GameRules.MaxTokens), HudCircle.RowHeight);

        for (int i = 0; i < GameRules.MaxTokens; i++)
        {
            Panel circle = HudCircle.CreateCircle(this, new Vector2(i * (HudCircle.CircleSize + HudCircle.Spacing), 0f));
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

            _tokenBadges[i] = HudCircle.CreateBadge(circle, i + 1);
        }

        EventBus.Instance.TokensChanged += Refresh;
        EventBus.Instance.TokenPickModeChanged += OnPickModeChanged;
        Refresh();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.TokensChanged -= Refresh;
        EventBus.Instance.TokenPickModeChanged -= OnPickModeChanged;
    }

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

            _tokenCircles[i].AddThemeStyleboxOverride("panel", HudCircle.BuildCircularStyle(fill, border));
            _tokenNameLabels[i].Text = token != null
                ? (string.IsNullOrEmpty(token.Name) ? token.Spell?.Name ?? "" : token.Name)
                : "";

            _tokenBadges[i].Visible = _pickModeActive;
        }
    }
}
