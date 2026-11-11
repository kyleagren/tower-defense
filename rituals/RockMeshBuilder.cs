using Godot;

// Low-poly rocks: an icosahedron with its corners pushed in and out at random, so no two match.
public static class RockMeshBuilder
{
    private const float GoldenRatio = 1.618034f;

    private static readonly Vector3[] _corners =
    {
        new(-1f, GoldenRatio, 0f), new(1f, GoldenRatio, 0f), new(-1f, -GoldenRatio, 0f), new(1f, -GoldenRatio, 0f),
        new(0f, -1f, GoldenRatio), new(0f, 1f, GoldenRatio), new(0f, -1f, -GoldenRatio), new(0f, 1f, -GoldenRatio),
        new(GoldenRatio, 0f, -1f), new(GoldenRatio, 0f, 1f), new(-GoldenRatio, 0f, -1f), new(-GoldenRatio, 0f, 1f)
    };

    private static readonly int[] _faces =
    {
        0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
        1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
        3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
        4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
    };

    // size is the rock's half-extent on each axis; roughness is how far, as a fraction, each corner may
    // move in or out.
    public static ArrayMesh Rock(Vector3 size, float roughness, RandomNumberGenerator rng)
    {
        Vector3[] corners = new Vector3[_corners.Length];

        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = _corners[i].Normalized() * (1f + rng.RandfRange(-roughness, roughness)) * size;
        }

        SurfaceTool surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetSmoothGroup(uint.MaxValue);

        for (int i = 0; i < _faces.Length; i += 3)
        {
            // The table winds counter-clockwise; Godot treats clockwise as front-facing.
            surface.AddVertex(corners[_faces[i]]);
            surface.AddVertex(corners[_faces[i + 2]]);
            surface.AddVertex(corners[_faces[i + 1]]);
        }

        surface.GenerateNormals();
        return surface.Commit();
    }
}
