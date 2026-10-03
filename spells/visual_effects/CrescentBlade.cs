using Godot;
using System.Collections.Generic;

// A crescent blade built in code rather than authored: a lens-shaped cross-section swept across the
// blade's span (local Y, -0.5 to 0.5), its middle bowed toward the flight (local -Z), pinched to a
// point at each tip. Its height runs along local X, which the Projectile turns upright. Built once per
// shape and shared, since every blade of a spell is the same.
public partial class CrescentBlade : MeshInstance3D
{
    [Export] public int Segments { get; private set; } = 24;
    // How far the middle leads the tips, along the flight.
    [Export] public float Bend { get; private set; } = 2f;
    // Front-to-back thickness at the middle.
    [Export] public float Thickness { get; private set; } = 1.6f;
    // Top-to-bottom height at the middle.
    [Export] public float Height { get; private set; } = 1.2f;

    // Keeps the tips' sections from collapsing to a single point, which would leave triangles with
    // no area and no usable normal.
    private const float MinTaper = 0.001f;

    private static readonly Dictionary<(int, float, float, float), ArrayMesh> _built = new();

    public override void _Ready()
    {
        (int, float, float, float) shape = (Segments, Bend, Thickness, Height);

        if (!_built.TryGetValue(shape, out ArrayMesh mesh))
        {
            mesh = Build();
            _built[shape] = mesh;
        }

        Mesh = mesh;
    }

    private ArrayMesh Build()
    {
        SurfaceTool surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        Vector3[] previous = Section(0);

        for (int i = 1; i <= Segments; i++)
        {
            Vector3[] next = Section(i);

            for (int corner = 0; corner < 4; corner++)
            {
                int following = (corner + 1) % 4;
                AddQuad(surface, previous[corner], previous[following], next[following], next[corner]);
            }

            previous = next;
        }

        surface.GenerateNormals();
        return surface.Commit();
    }

    // The cross-section partway across the span: front and back edges along the flight, top and
    // bottom along the height. Both shrink toward the tips, the height more gently.
    private Vector3[] Section(int index)
    {
        float across = (float)index / Segments;
        float taper = Mathf.Max(Mathf.Sin(across * Mathf.Pi), MinTaper);
        float y = across - 0.5f;
        float centre = Bend * (0.5f - taper);
        float halfThickness = Thickness * 0.5f * taper;
        float halfHeight = Height * 0.5f * Mathf.Sqrt(taper);

        return new[]
        {
            new Vector3(0f, y, centre - halfThickness),
            new Vector3(halfHeight, y, centre),
            new Vector3(0f, y, centre + halfThickness),
            new Vector3(-halfHeight, y, centre)
        };
    }

    private static void AddQuad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        surface.AddVertex(a);
        surface.AddVertex(b);
        surface.AddVertex(c);
        surface.AddVertex(a);
        surface.AddVertex(c);
        surface.AddVertex(d);
    }
}
