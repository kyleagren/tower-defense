using Godot;

// The field HUD's 4-slot ability row, sibling to TokenBar under AbilityHud. Reads Sentinel.Local's
// AbilitySlots fresh every frame rather than through an event - CooldownFraction and IsActive are
// continuous quantities with no "changed" signal of their own, the same reason
// ItemDragController's cursor icon and CharacterSheetPanel's live Shield row also poll per frame.
// On keyboard, the slot badges only show outside pick mode, deciding that independently off the
// same TokenPickModeChanged signal TokenBar reacts to, and not while the Architect is active, since
// 1/2/3 reach its tokens. On a controller the row's L1 chip shows and the badges always do, since
// the face buttons never collide with tokens.
public partial class AbilityBar : Control
{
    private const int SlotCount = 4;
    private static readonly string[] _abilityActions = { "ability_1", "ability_2", "ability_3", "ability_4" };

    // Placeholder palette, not yet rarity/element-coded - abilities have no equivalent of a
    // spell's element to color by. The base circle always stays _emptyColor; CooldownOverlay draws
    // the growing radial wipe on top of it in _readyColor (or _activeColor while active).
    private static readonly Color _emptyColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
    private static readonly Color _readyColor = new Color(0.25f, 0.4f, 0.55f, 0.9f);
    private static readonly Color _activeColor = new Color(0.85f, 0.75f, 0.15f);

    private readonly Label[] _nameLabels = new Label[SlotCount];
    private readonly PromptBadge[] _badges = new PromptBadge[SlotCount];
    private readonly CooldownOverlay[] _cooldownOverlays = new CooldownOverlay[SlotCount];
    private PromptBadge _chip;

    private bool _pickModeActive;
    private bool _architectActive;

    private static bool ArchitectActive => LevelContext.Current?.LocalActiveCharacter is Architect;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(HudCircle.RowWidth(SlotCount), HudCircle.RowHeight);

        _chip = HudCircle.CreateChip(this);

        for (int i = 0; i < SlotCount; i++)
        {
            Panel circle = HudCircle.CreateCircle(this, new Vector2(HudCircle.SlotX(i), 0f));
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

            _badges[i] = HudCircle.CreateBadge(circle);
        }

        EventBus.Instance.TokenPickModeChanged += OnPickModeChanged;
        InputDevice.Changed += UpdateBadges;
        UpdateBadges();
        UpdateRow();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.TokenPickModeChanged -= OnPickModeChanged;
        InputDevice.Changed -= UpdateBadges;
    }

    public override void _Process(double delta)
    {
        UpdateRow();

        // Nothing signals a switch between the Architect and a Sentinel, so it's watched here.
        if (ArchitectActive != _architectActive)
        {
            _architectActive = ArchitectActive;
            UpdateBadges();
        }
    }

    private void OnPickModeChanged(bool active)
    {
        _pickModeActive = active;
        UpdateBadges();
    }

    // L1 only exists on a controller; keyboard has no ability modifier, and shares 1/2/3 with pick mode.
    private void UpdateBadges()
    {
        _chip.ShowAction("ability_modifier");
        _chip.Visible = InputDevice.UsingController;

        bool showBadges = InputDevice.UsingController || (!_pickModeActive && !_architectActive);

        for (int i = 0; i < SlotCount; i++)
        {
            _badges[i].ShowAction(_abilityActions[i]);
            _badges[i].Visible = showBadges;
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
            Ability tap = sentinel != null && i < sentinel.AbilitySlots.Count ? sentinel.AbilitySlots[i] : null;
            Ability hold = sentinel != null && i < sentinel.HoldAbilitySlots.Count ? sentinel.HoldAbilitySlots[i] : null;

            if (tap == null || !IsInstanceValid(tap))
            {
                _cooldownOverlays[i].Progress = 0f;
                _nameLabels[i].Text = "";
                continue;
            }

            bool holdValid = hold != null && IsInstanceValid(hold);
            Ability active = tap.IsActive ? tap : holdValid && hold.IsActive ? hold : null;
            float cooldown = Mathf.Max(tap.CooldownFraction, holdValid ? hold.CooldownFraction : 0f);

            _cooldownOverlays[i].FillColor = active != null ? _activeColor : _readyColor;
            _cooldownOverlays[i].Progress = active != null ? active.ActiveFraction : 1f - cooldown;
            _nameLabels[i].Text = sentinel.SlotDisplayName(i);
        }
    }
}
