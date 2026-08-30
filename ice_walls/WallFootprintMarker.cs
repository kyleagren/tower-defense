using Godot;

public partial class WallFootprintMarker : MeshInstance3D
{
    private static readonly StandardMaterial3D _material = new StandardMaterial3D
    {
        AlbedoColor = new Color(0.6f, 0.9f, 1f, 0.8f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    // A thin ice-colored outline on the ground tracing the wall's own footprint. Start/end are
    // given in world space and converted to this node's local space so it renders correctly
    // regardless of where its parent (the owning Ritual) is positioned.
    public void Initialize(Vector3 start, Vector3 end, float segmentWidth)
    {
        Vector3 localStart = ToLocal(start);
        Vector3 localEnd = ToLocal(end);

        const float BorderThickness = 0.08f;
        float halfWidth = segmentWidth / 2f;

        Vector3 diff = localEnd - localStart;
        Vector3 direction = diff.LengthSquared() > 0.0001f ? diff.Normalized() : Vector3.Forward;
        Vector3 perpendicular = new Vector3(-direction.Z, 0f, direction.X);
        Vector3 up = Vector3.Up * 0.02f;

        Vector3 innerA = localStart + perpendicular * halfWidth + up;
        Vector3 innerB = localEnd + perpendicular * halfWidth + up;
        Vector3 innerC = localEnd - perpendicular * halfWidth + up;
        Vector3 innerD = localStart - perpendicular * halfWidth + up;

        Vector3 outerA = innerA + perpendicular * BorderThickness - direction * BorderThickness;
        Vector3 outerB = innerB + perpendicular * BorderThickness + direction * BorderThickness;
        Vector3 outerC = innerC - perpendicular * BorderThickness + direction * BorderThickness;
        Vector3 outerD = innerD - perpendicular * BorderThickness - direction * BorderThickness;

        SurfaceTool surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        AddBorderQuad(surfaceTool, outerA, outerB, innerB, innerA);
        AddBorderQuad(surfaceTool, outerD, outerC, innerC, innerD);
        AddBorderQuad(surfaceTool, outerA, outerD, innerD, innerA);
        AddBorderQuad(surfaceTool, outerB, outerC, innerC, innerB);

        Mesh = surfaceTool.Commit();
        MaterialOverride = _material;
    }

    private static void AddBorderQuad(SurfaceTool surfaceTool, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        surfaceTool.AddVertex(a);
        surfaceTool.AddVertex(b);
        surfaceTool.AddVertex(c);

        surfaceTool.AddVertex(a);
        surfaceTool.AddVertex(c);
        surfaceTool.AddVertex(d);
    }
}
