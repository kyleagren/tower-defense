using Godot;

public partial class WallFootprintMarker : MeshInstance3D
{
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

        Vector3[] inner =
        {
            localStart + perpendicular * halfWidth + up,
            localEnd + perpendicular * halfWidth + up,
            localEnd - perpendicular * halfWidth + up,
            localStart - perpendicular * halfWidth + up
        };

        Vector3[] outer =
        {
            inner[0] + perpendicular * BorderThickness - direction * BorderThickness,
            inner[1] + perpendicular * BorderThickness + direction * BorderThickness,
            inner[2] - perpendicular * BorderThickness + direction * BorderThickness,
            inner[3] - perpendicular * BorderThickness - direction * BorderThickness
        };

        SurfaceTool surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        MeshBuilder.AddBorderRing(surfaceTool, inner, outer);

        Mesh = surfaceTool.Commit();
        MaterialOverride = PreviewMaterials.WallFootprint;
    }
}
