using Godot;

public partial class PlayerShieldBar : TextureProgressBar
{
    [Export] public int BarWidth = 56;
    [Export] public int BarHeight = 176;
    [Export] public int CornerRadius = 12;
    [Export] public float BorderThickness = 4f;
    [Export] public Color BorderColor = new Color(0.08f, 0.18f, 0.2f);
    [Export] public Color EmptyColor = new Color(0.12f, 0.22f, 0.26f);
    [Export] public Color FillColor = new Color(0.25f, 0.72f, 0.84f);
    [Export] public Color OvershieldColor = new Color(0.62f, 0.3f, 0.85f);
    [Export] public Label ShieldLabel { get; private set; }
    [Export] public TextureProgressBar OvershieldFill { get; private set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(BarWidth, BarHeight);
        FillMode = (int)FillModeEnum.BottomToTop;
        MinValue = 0f;
        MaxValue = 1f;
        Step = 0f;
        Value = 1f;

        TextureUnder = HudBarTexture.Build(BarWidth, BarHeight, CornerRadius, BorderThickness, BorderColor, EmptyColor);
        TextureProgress = HudBarTexture.Build(BarWidth, BarHeight, CornerRadius, BorderThickness, BorderColor, FillColor);

        OvershieldFill.FillMode = (int)FillModeEnum.TopToBottom;
        OvershieldFill.MinValue = 0f;
        OvershieldFill.MaxValue = 1f;
        OvershieldFill.Step = 0f;
        OvershieldFill.Value = 0f;
        OvershieldFill.TextureProgress = HudBarTexture.Build(BarWidth, BarHeight, CornerRadius, BorderThickness, BorderColor, OvershieldColor);

        EventBus.Instance.PlayerShieldChanged += OnPlayerShieldChanged;

        // Sentinel._Ready (which emits the initial PlayerShieldChanged) can run before this node's
        // own _Ready subscribes, depending on scene tree order - pull the current value directly
        // as a fallback, same as PlayerHealthOrb does for health.
        if (Sentinel.Local != null)
        {
            ShowShield(Sentinel.Local.CurrentShield, Sentinel.Local.MaxShield);
        }
    }

    public override void _ExitTree()
    {
        EventBus.Instance.PlayerShieldChanged -= OnPlayerShieldChanged;
    }

    // Every Sentinel reports through the same signal; this bar is the local player's.
    private void OnPlayerShieldChanged(PlayerId player, float currentShield, float maxShield)
    {
        if (player == PlayerProfiles.LocalId)
        {
            ShowShield(currentShield, maxShield);
        }
    }

    // Overshield fills purple from the top down over the full blue bar, as a fraction of MaxShield -
    // so at the 2x ceiling the whole bar is purple.
    private void ShowShield(float currentShield, float maxShield)
    {
        Value = maxShield > 0f ? Mathf.Clamp(currentShield / maxShield, 0f, 1f) : 0f;
        OvershieldFill.Value = maxShield > 0f ? Mathf.Clamp((currentShield - maxShield) / maxShield, 0f, 1f) : 0f;
        ShieldLabel.Text = $"{Mathf.CeilToInt(currentShield)}/{Mathf.CeilToInt(maxShield)}";
    }
}
