using Godot;

// A pie-slice radial wipe drawn on top of a HudCircle slot - custom-drawn rather than a
// TextureProgressBar since every other HUD circle here is a procedural StyleBox, not a texture
// asset. Sweeps clockwise from the top, matching a clock face.
public partial class CooldownOverlay : Control
{
    private const float StartAngle = -Mathf.Pi / 2f;
    private const int ArcSegments = 48;

    private float _progress;
    private Color _fillColor = Colors.White;

    // 0 draws nothing; 1 fills the full circle.
    public float Progress
    {
        get => _progress;
        set
        {
            float clamped = Mathf.Clamp(value, 0f, 1f);
            if (Mathf.IsEqualApprox(clamped, _progress))
            {
                return;
            }

            _progress = clamped;
            QueueRedraw();
        }
    }

    public Color FillColor
    {
        get => _fillColor;
        set
        {
            if (_fillColor == value)
            {
                return;
            }

            _fillColor = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (Progress <= 0f)
        {
            return;
        }

        Vector2 center = Size / 2f;
        float radius = Mathf.Min(Size.X, Size.Y) / 2f;
        float endAngle = StartAngle + Progress * Mathf.Tau;
        int segments = Mathf.Max(1, Mathf.CeilToInt(ArcSegments * Progress));

        Vector2[] points = new Vector2[segments + 2];
        points[0] = center;

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(StartAngle, endAngle, (float)i / segments);
            points[i + 1] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        DrawColoredPolygon(points, FillColor);
    }
}
