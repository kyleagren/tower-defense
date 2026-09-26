using Godot;

// Placeholder for Shield Slam's shockwave: a flat translucent fan on the ground covering exactly the
// cone the slam hit, fading out. Built in code at the slam's actual size rather than scaling an
// authored mesh.
public partial class ShieldSlamWave : MeshInstance3D
{
    [Export] public float Lifetime { get; private set; } = 0.3f;
    [Export] public Color WaveColor { get; private set; } = new Color(1f, 0.8f, 0.4f, 0.4f);

    private const int Segments = 16;
    // Just above the ground, so the fan doesn't z-fight the floor it lies on.
    private const float GroundOffset = 0.05f;

    // Must already be in the tree - it positions itself globally.
    public void Play(Vector3 origin, Vector3 forward, float range, float coneDegrees)
    {
        GlobalPosition = origin + Vector3.Up * GroundOffset;
        Mesh = BuildFan(forward, range, Mathf.DegToRad(coneDegrees));
        CastShadow = ShadowCastingSetting.Off;

        StandardMaterial3D material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = WaveColor
        };
        MaterialOverride = material;

        Tween tween = CreateTween();
        tween.TweenProperty(material, "albedo_color:a", 0f, Lifetime);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    private static ArrayMesh BuildFan(Vector3 forward, float range, float coneRadians)
    {
        SurfaceTool surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < Segments; i++)
        {
            float from = -coneRadians / 2f + coneRadians * i / Segments;
            float to = -coneRadians / 2f + coneRadians * (i + 1) / Segments;

            surface.AddVertex(Vector3.Zero);
            surface.AddVertex(forward.Rotated(Vector3.Up, from) * range);
            surface.AddVertex(forward.Rotated(Vector3.Up, to) * range);
        }

        return surface.Commit();
    }
}
