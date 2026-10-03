using Godot;

// Where a Swirl burst went off: a handful of short curved strokes in the swirled element's color,
// scattered around the centre and all turning the same way, each drawn from end to end and then
// faded - wind whipping round in a small vortex.
public partial class SwirlVortex : Node3D
{
    // A flat unit card lying on the ground, and the stroke material drawn on it.
    [Export] public Mesh StrokeMesh { get; private set; }
    [Export] public Material StrokeMaterial { get; private set; }
    [Export] public int StrokeCount = 7;
    // How long one stroke takes to draw, end to end.
    [Export] public float DrawTime = 0.15f;
    // Strokes start somewhere within this window, so they don't all draw in lockstep.
    [Export] public float Stagger = 0.15f;
    [Export] public float FadeTime = 0.15f;

    // Each stroke's distance from the centre, as a share of the swirl's radius.
    private const float MinDistance = 0.35f;
    private const float MaxDistance = 0.9f;

    // A stroke is about as long as the arc it sits on, and half that wide to leave room for its bow.
    private const float LengthToDistance = 1.1f;
    private const float WidthToLength = 0.5f;

    // The highest a stroke sits on the dome, in degrees above the ground.
    private const float MaxElevationDegrees = 70f;

    // Up near the top, where the circles round the dome get small, strokes keep at least this share
    // of their distance as length.
    private const float MinLengthShare = 0.3f;

    public void Show(Vector3 position, float radius, Color color)
    {
        GlobalPosition = position;

        for (int i = 0; i < StrokeCount; i++)
        {
            SpawnStroke(radius, color);
        }

        GetTree().CreateTimer(Stagger + DrawTime + FadeTime).Timeout += QueueFree;
    }

    private void SpawnStroke(float radius, Color color)
    {
        float angle = (float)GD.RandRange(0.0, Mathf.Tau);
        float elevation = Mathf.DegToRad((float)GD.RandRange(0.0, MaxElevationDegrees));
        float distance = radius * (float)GD.RandRange(MinDistance, MaxDistance);
        Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        // Every stroke runs the same way round the vertical axis. On a dome over the centre rather than
        // flat on the ground: the card lies against the dome's surface, facing straight out from the
        // centre, with its width running up the dome.
        Vector3 tangent = new Vector3(-outward.Z, 0f, outward.X);
        Vector3 radial = outward * Mathf.Cos(elevation) + Vector3.Up * Mathf.Sin(elevation);
        Vector3 upDome = Vector3.Up * Mathf.Cos(elevation) - outward * Mathf.Sin(elevation);
        float length = distance * Mathf.Max(Mathf.Cos(elevation), MinLengthShare) * LengthToDistance;

        MeshInstance3D stroke = new MeshInstance3D
        {
            Mesh = StrokeMesh,
            MaterialOverride = StrokeMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(stroke);

        stroke.Position = radial * distance + Vector3.Up * 0.1f;
        stroke.Basis = new Basis(tangent * length, radial, upDome * length * WidthToLength);
        stroke.SetInstanceShaderParameter("element_color", color);
        stroke.SetInstanceShaderParameter("head", 0f);
        stroke.SetInstanceShaderParameter("opacity", 1f);

        Tween tween = CreateTween();
        tween.TweenInterval(GD.Randf() * Stagger);
        tween.TweenMethod(Callable.From<float>(head => stroke.SetInstanceShaderParameter("head", head)), 0f, 1f, DrawTime);
        tween.TweenMethod(Callable.From<float>(opacity => stroke.SetInstanceShaderParameter("opacity", opacity)), 1f, 0f, FadeTime);
    }
}
