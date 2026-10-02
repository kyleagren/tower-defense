using Godot;

// Flat placeholder shapes for ability visuals lying on the ground, built in code at their real size.
public static class GroundMesh
{
    private const int Segments = 48;

    // Each builder appends a surface to `into` when given one, so one mesh can carry several
    // differently colored parts.
    public static ArrayMesh Disc(float radius, ArrayMesh into = null)
    {
        SurfaceTool surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < Segments; i++)
        {
            surface.AddVertex(Vector3.Zero);
            surface.AddVertex(EdgeDirection(i) * radius);
            surface.AddVertex(EdgeDirection(i + 1) * radius);
        }

        return surface.Commit(into);
    }

    public static ArrayMesh Ring(float innerRadius, float outerRadius, ArrayMesh into = null)
    {
        SurfaceTool surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < Segments; i++)
        {
            Vector3 from = EdgeDirection(i);
            Vector3 to = EdgeDirection(i + 1);

            surface.AddVertex(from * innerRadius);
            surface.AddVertex(from * outerRadius);
            surface.AddVertex(to * outerRadius);
            surface.AddVertex(from * innerRadius);
            surface.AddVertex(to * outerRadius);
            surface.AddVertex(to * innerRadius);
        }

        return surface.Commit(into);
    }

    public static StandardMaterial3D Material(Color color) => new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        AlbedoColor = color
    };

    private static Vector3 EdgeDirection(int segment) =>
        Vector3.Forward.Rotated(Vector3.Up, Mathf.Tau * segment / Segments);
}
