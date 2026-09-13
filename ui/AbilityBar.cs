using Godot;

// The field HUD's 4-slot ability row, sibling to TokenBar under AbilityHud. Reads Sentinel.Local's
// AbilitySlots fresh every frame rather than through an event - CooldownFraction and IsActive are
// continuous quantities with no "changed" signal of their own, the same reason
// ItemDragController's cursor icon and CharacterSheetPanel's live Shield row also poll per frame.
// Number badges only show outside pick mode, deciding that independently off the same
// TokenPickModeChanged signal TokenBar reacts to.
public partial class AbilityBar : Control
{
    private const int SlotCount = 4;

    // Placeholder palette, not yet rarity/element-coded - abilities have no equivalent of a
    // spell's element to color by. The base circle always stays _emptyColor; CooldownOverlay draws
    // the growing radial wipe on top of it in _readyColor (or _activeColor while active).
    private static readonly Color _emptyColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
    private static readonly Color _readyColor = new Color(0.25f, 0.4f, 0.55f, 0.9f);
    private static readonly Color _activeColor = new Color(0.85f, 0.75f, 0.15f);

    private readonly Label[] _nameLabels = new Label[SlotCount];
    private readonly Panel[] _badges = new Panel[SlotCount];
    private readonly CooldownOverlay[] _cooldownOverlays = new CooldownOverlay[SlotCount];

    private bool _pickModeActive;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(HudCircle.RowWidth(SlotCount), HudCircle.RowHeight);

        for (int i = 0; i < SlotCount; i++)
        {
            Panel circle = HudCircle.CreateCircle(this, new Vector2(i * (HudCircle.CircleSize + HudCircle.Spacing), 0f));
            circle.AddThemeStyleboxOverride("panel", HudCircle.BuildCircularStyle(_emptyColor));

            _cooldownOverlays[i] = HudCircle.CreateCooldownOverlay(circle);

            Label nameLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 11);
            nameLabel.AddThemeColorOverride("font_color", Colors.White);
            nameLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            circle.AddChild(nameLabel);
            _nameLabels[i] = nameLabel;

            _badges[i] = HudCircle.CreateBadge(circle, i + 1);
        }

        EventBus.Instance.TokenPickModeChanged += OnPickModeChanged;
        UpdateBadgeVisibility();
        UpdateRow();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.TokenPickModeChanged -= OnPickModeChanged;
    }

    public override void _Process(double delta)
    {
        UpdateRow();
    }

    private void OnPickModeChanged(bool active)
    {
        _pickModeActive = active;
        UpdateBadgeVisibility();
    }

    private void UpdateBadgeVisibility()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            _badges[i].Visible = !_pickModeActive;
        }
    }

    // Reads Sentinel.Local fresh each call rather than caching AbilitySlots once - the active
    // Sentinel can swap out from under this (hub character select), and re-fetching is cheap
    // enough at 4 slots a frame that there's no stale reference to guard against.
    private void UpdateRow()
    {
        Sentinel sentinel = Sentinel.Local;

        for (int i = 0; i < SlotCount; i++)
        {
            Ability ability = sentinel != null && i < sentinel.AbilitySlots.Count ? sentinel.AbilitySlots[i] : null;

            if (ability == null || !IsInstanceValid(ability))
            {
                _cooldownOverlays[i].Progress = 0f;
                _nameLabels[i].Text = "";
                continue;
            }

            _cooldownOverlays[i].FillColor = ability.IsActive ? _activeColor : _readyColor;
            _cooldownOverlays[i].Progress = ability.IsActive ? ability.ActiveFraction : 1f - ability.CooldownFraction;
            _nameLabels[i].Text = ability.DisplayName;
        }
    }
}
