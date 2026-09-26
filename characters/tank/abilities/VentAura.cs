using Godot;

// Vent's placeholder: a faint disc on the ground around the Tank with a crisp ring at its edge, so
// it still reads apart from Retaliation's circle when the two overlap.
public partial class VentAura : MeshInstance3D
{
    [Export] public Color FillColor { get; private set; } = new Color(1f, 0.65f, 0.2f, 0.12f);
    [Export] public Color RingColor { get; private set; } = new Color(1f, 0.75f, 0.3f, 0.85f);
    [Export] public float RingWidth { get; private set; } = 0.12f;

    private const int Segments = 48;
    // A touch above Retaliation's circle, so the two don't z-fight when both are down.
    private const float GroundOffset = 0.07f;

    // Must be parented to the Tank - it positions itself locally and rides along with him.
    public void Begin(float radius)
    {
        Position = Vector3.Up * GroundOffset;
        Mesh = BuildMesh(radius);
        CastShadow = ShadowCastingSetting.Off;
        SetSurfaceOverrideMaterial(0, BuildMaterial(FillColor));
        SetSurfaceOverrideMaterial(1, BuildMaterial(RingColor));
    }

    // Surface 0 is the fill, surface 1 the ring around it.
    private ArrayMesh BuildMesh(float radius)
    {
        float inner = Mathf.Max(radius - RingWidth, 0f);
        SurfaceTool fill = new SurfaceTool();
        SurfaceTool ring = new SurfaceTool();
        fill.Begin(Mesh.PrimitiveType.Triangles);
        ring.Begin(Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < Segments; i++)
        {
            Vector3 from = EdgeDirection(i);
            Vector3 to = EdgeDirection(i + 1);

            fill.AddVertex(Vector3.Zero);
            fill.AddVertex(from * inner);
            fill.AddVertex(to * inner);

            ring.AddVertex(from * inner);
            ring.AddVertex(from * radius);
            ring.AddVertex(to * radius);
            ring.AddVertex(from * inner);
            ring.AddVertex(to * radius);
            ring.AddVertex(to * inner);
        }

        ArrayMesh mesh = fill.Commit();
        ring.Commit(mesh);
        return mesh;
    }

    private static Vector3 EdgeDirection(int segment) =>
        Vector3.Forward.Rotated(Vector3.Up, Mathf.Tau * segment / Segments);

    private static StandardMaterial3D BuildMaterial(Color color) => new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        AlbedoColor = color
    };
}
