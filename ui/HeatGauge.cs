using Godot;

// The Tank's heat dial: fills outward from a center line - up for the heat side, down for the armor
// side - since equilibrium sits in the middle rather than at an end. The fill is plain rectangles
// drawn inside Mask, which clips them to the pill's shape, so the ends follow the pill's own curve
// rather than rounding the fill into a smaller pill of its own. The heat side is cut from a fixed
// red-to-orange gradient, so how much of it shows says how hot the Tank is. Hidden for any Sentinel
// without heat, and polled per frame for the same reason AbilityGauge is.
public partial class HeatGauge : Control
{
    [Export] public int BarWidth = 80;
    [Export] public int BarHeight = 176;
    [Export] public int BorderThickness = 4;
    [Export] public Color BorderColor = new Color(0.1f, 0.1f, 0.12f);
    [Export] public Color EmptyColor = new Color(0.16f, 0.16f, 0.18f);
    [Export] public Color HeatLowColor = new Color(0.85f, 0.1f, 0.05f);
    [Export] public Color HeatHighColor = new Color(1f, 0.55f, 0.1f);
    [Export] public Color ArmorColor = new Color(0.8f, 0.68f, 0.5f);
    [Export] public Color EquilibriumColor = new Color(1f, 1f, 1f, 0.6f);
    [Export] public Panel Mask { get; private set; }
    [Export] public Control Fill { get; private set; }
    [Export] public Label HeatLabel { get; private set; }

    // Tall enough that the gradient doesn't visibly band across the bar's height.
    private const int GradientResolution = 128;

    // -1 to 1.
    private float _fraction;
    private GradientTexture2D _heatGradient;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(BarWidth, BarHeight);
        MouseFilter = MouseFilterEnum.Ignore;

        int innerRadius = BarWidth / 2 - BorderThickness;
        Mask.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = EmptyColor,
            CornerRadiusTopLeft = innerRadius,
            CornerRadiusTopRight = innerRadius,
            CornerRadiusBottomLeft = innerRadius,
            CornerRadiusBottomRight = innerRadius
        });
        Mask.ClipChildren = ClipChildrenMode.AndDraw;

        // Bottom (the center line) to top (+100), red to orange.
        _heatGradient = new GradientTexture2D
        {
            Gradient = new Gradient { Colors = [HeatLowColor, HeatHighColor] },
            FillFrom = new Vector2(0f, 1f),
            FillTo = new Vector2(0f, 0f),
            Width = 1,
            Height = GradientResolution
        };

        Fill.Draw += DrawFill;
    }

    public override void _Process(double delta)
    {
        Tank tank = Sentinel.Local as Tank;
        Visible = tank != null;

        if (tank == null)
        {
            return;
        }

        float fraction = tank.Heat / Tank.HeatLimit;
        if (!Mathf.IsEqualApprox(fraction, _fraction))
        {
            _fraction = fraction;
            Fill.QueueRedraw();
        }

        int heat = Mathf.RoundToInt(tank.Heat);
        HeatLabel.Text = heat > 0 ? $"+{heat}" : heat.ToString();
    }

    public override void _Draw()
    {
        int outerRadius = BarWidth / 2;
        DrawStyleBox(new StyleBoxFlat
        {
            DrawCenter = false,
            BorderColor = BorderColor,
            BorderWidthLeft = BorderThickness,
            BorderWidthTop = BorderThickness,
            BorderWidthRight = BorderThickness,
            BorderWidthBottom = BorderThickness,
            CornerRadiusTopLeft = outerRadius,
            CornerRadiusTopRight = outerRadius,
            CornerRadiusBottomLeft = outerRadius,
            CornerRadiusBottomRight = outerRadius
        }, new Rect2(0f, 0f, BarWidth, BarHeight));
    }

    // Drawn onto Fill rather than this node, so Mask clips it to the pill.
    private void DrawFill()
    {
        Vector2 size = Fill.Size;
        float center = size.Y / 2f;
        float fillHeight = center * Mathf.Abs(_fraction);

        if (_fraction > 0f)
        {
            // Only the stretch of gradient the current heat has reached - the red end sits at the
            // center line, so a little heat shows red and a lot shades up into orange.
            Rect2 source = new Rect2(0f, GradientResolution * (1f - _fraction), 1f, GradientResolution * _fraction);
            Fill.DrawTextureRectRegion(_heatGradient, new Rect2(0f, center - fillHeight, size.X, fillHeight), source);
        }
        else if (_fraction < 0f)
        {
            Fill.DrawRect(new Rect2(0f, center, size.X, fillHeight), ArmorColor);
        }

        Fill.DrawLine(new Vector2(0f, center), new Vector2(size.X, center), EquilibriumColor, 2f);
    }
}
