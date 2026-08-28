using Godot;

public partial class SigilPreview : Control
{
    [Export] public Line2D SigilLine { get; private set; }
    [Export] public Line2D StartFoot { get; private set; }
    [Export] public Line2D EndFoot { get; private set; }
    [Export] public LineEdit NameInput { get; private set; }
    [Export] public float Radius = 180f;
    [Export] public float FootLength = 14f;
    [Export] public float FlareAngleDegrees = 55f;

    public override void _Ready()
    {
        NameInput.TextChanged += OnTextChanged;

        Vector2 center = Size / 2f;
        SigilLine.Position = center;
        StartFoot.Position = center;
        EndFoot.Position = center;

        OnTextChanged(NameInput.Text);
    }

    private void OnTextChanged(string newText)
    {
        Vector2[] points = SigilGenerator.GeneratePoints(newText, Vector2.Zero, Radius);
        SigilLine.Points = points;

        UpdateFoot(StartFoot, points, fromStart: true);
        UpdateFoot(EndFoot, points, fromStart: false);
    }

    private void UpdateFoot(Line2D foot, Vector2[] points, bool fromStart)
    {
        if (points.Length < 2)
        {
            foot.Points = System.Array.Empty<Vector2>();
            return;
        }

        Vector2 tip = fromStart ? points[0] : points[^1];
        Vector2 neighbor = fromStart ? points[1] : points[^2];
        Vector2 direction = (tip - neighbor).Normalized();

        Vector2 armA = tip + direction.Rotated(Mathf.DegToRad(FlareAngleDegrees)) * FootLength;
        Vector2 armB = tip + direction.Rotated(-Mathf.DegToRad(FlareAngleDegrees)) * FootLength;

        foot.Points = new[] { armA, tip, armB };
    }
}
