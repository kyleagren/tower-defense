using Godot;

public partial class PlayerHealthOrb : TextureProgressBar
{
    [Export] public int Diameter = 192;
    [Export] public float BorderThickness = 4f;
    [Export] public Color BorderColor = new Color(0.14f, 0.09f, 0.07f);
    [Export] public Color EmptyColor = new Color(0.22f, 0.06f, 0.06f);
    [Export] public Color FillColor = new Color(0.65f, 0.11f, 0.11f);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Diameter, Diameter);
        FillMode = (int)FillModeEnum.BottomToTop;
        MinValue = 0f;
        MaxValue = 1f;
        Value = 1f;

        TextureUnder = BuildCircleTexture(EmptyColor);
        TextureProgress = BuildCircleTexture(FillColor);

        EventBus.Instance.PlayerHealthChanged += OnPlayerHealthChanged;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.PlayerHealthChanged -= OnPlayerHealthChanged;
    }

    private void OnPlayerHealthChanged(float currentHealth, float maxHealth)
    {
        Value = maxHealth > 0f ? Mathf.Clamp(currentHealth / maxHealth, 0f, 1f) : 0f;
    }

    private ImageTexture BuildCircleTexture(Color fill)
    {
        Image image = Image.CreateEmpty(Diameter, Diameter, false, Image.Format.Rgba8);
        Vector2 center = new Vector2(Diameter, Diameter) / 2f;
        float radius = Diameter / 2f;

        for (int y = 0; y < Diameter; y++)
        {
            for (int x = 0; x < Diameter; x++)
            {
                float distance = new Vector2(x + 0.5f, y + 0.5f).DistanceTo(center);
                Color pixel = distance <= radius - BorderThickness ? fill
                    : distance <= radius ? BorderColor
                    : Colors.Transparent;

                image.SetPixel(x, y, pixel);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
