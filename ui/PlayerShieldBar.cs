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
    [Export] public Label ShieldLabel { get; private set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(BarWidth, BarHeight);
        FillMode = (int)FillModeEnum.BottomToTop;
        MinValue = 0f;
        MaxValue = 1f;
        Step = 0f;
        Value = 1f;

        TextureUnder = BuildBarTexture(EmptyColor);
        TextureProgress = BuildBarTexture(FillColor);

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

    private void ShowShield(float currentShield, float maxShield)
    {
        Value = maxShield > 0f ? Mathf.Clamp(currentShield / maxShield, 0f, 1f) : 0f;
        ShieldLabel.Text = $"{Mathf.CeilToInt(currentShield)}/{Mathf.CeilToInt(maxShield)}";
    }

    private ImageTexture BuildBarTexture(Color fill)
    {
        Image image = Image.CreateEmpty(BarWidth, BarHeight, false, Image.Format.Rgba8);

        for (int y = 0; y < BarHeight; y++)
        {
            for (int x = 0; x < BarWidth; x++)
            {
                bool insideOuter = IsInsideRoundedRect(x + 0.5f, y + 0.5f, 0f);
                bool insideFill = IsInsideRoundedRect(x + 0.5f, y + 0.5f, BorderThickness);
                Color pixel = insideFill ? fill : insideOuter ? BorderColor : Colors.Transparent;
                image.SetPixel(x, y, pixel);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private bool IsInsideRoundedRect(float x, float y, float inset)
    {
        float left = inset;
        float top = inset;
        float right = BarWidth - inset;
        float bottom = BarHeight - inset;
        float radius = Mathf.Max(0f, CornerRadius - inset);

        if (x < left || x > right || y < top || y > bottom)
        {
            return false;
        }

        float cx = Mathf.Clamp(x, left + radius, right - radius);
        float cy = Mathf.Clamp(y, top + radius, bottom - radius);

        return new Vector2(x, y).DistanceTo(new Vector2(cx, cy)) <= radius;
    }
}
