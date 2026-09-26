using Godot;

// The PlayStation face-button shapes and d-pad arrows, drawn rather than typed - font coverage of
// those symbols varies, and a drawn square can be sized to read as clearly as the others.
public partial class ButtonGlyph : Control
{
    public enum Shape { None, Cross, Circle, Square, Triangle, Up, Down, Left, Right }

    private const float LineWidth = 2f;
    // Share of the control each shape spans.
    private const float Extent = 0.5f;

    private Shape _shape;

    public Shape Current
    {
        get => _shape;
        set
        {
            _shape = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        Vector2 center = Size / 2f;
        float half = Mathf.Min(Size.X, Size.Y) * Extent / 2f;
        Color color = Colors.White;

        switch (_shape)
        {
            case Shape.Cross:
                DrawLine(center + new Vector2(-half, -half), center + new Vector2(half, half), color, LineWidth, true);
                DrawLine(center + new Vector2(-half, half), center + new Vector2(half, -half), color, LineWidth, true);
                break;
            case Shape.Circle:
                DrawArc(center, half, 0f, Mathf.Tau, 32, color, LineWidth, true);
                break;
            case Shape.Square:
                DrawRect(new Rect2(center - new Vector2(half, half), new Vector2(half, half) * 2f), color, false, LineWidth);
                break;
            case Shape.Triangle:
                Vector2[] outline =
                {
                    center + new Vector2(0f, -half),
                    center + new Vector2(half, half * 0.8f),
                    center + new Vector2(-half, half * 0.8f),
                    center + new Vector2(0f, -half)
                };
                DrawPolyline(outline, color, LineWidth, true);
                break;
            case Shape.Up:
            case Shape.Down:
            case Shape.Left:
            case Shape.Right:
                DrawColoredPolygon(ArrowPoints(center, half, ArrowAngle(_shape)), color);
                break;
        }
    }

    private static float ArrowAngle(Shape shape) => shape switch
    {
        Shape.Right => Mathf.Pi / 2f,
        Shape.Down => Mathf.Pi,
        Shape.Left => -Mathf.Pi / 2f,
        _ => 0f
    };

    // An up-pointing wedge, turned to face the arrow's direction.
    private static Vector2[] ArrowPoints(Vector2 center, float half, float angle)
    {
        return new[]
        {
            center + new Vector2(0f, -half).Rotated(angle),
            center + new Vector2(half, half * 0.6f).Rotated(angle),
            center + new Vector2(-half, half * 0.6f).Rotated(angle)
        };
    }
}
