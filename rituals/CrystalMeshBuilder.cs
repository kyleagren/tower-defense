using Godot;

// Crystal shapes built from a handful of vertices, so Rituals need no modelled assets.
public static class CrystalMeshBuilder
{
    // A ring of points at the origin's height, with one tip above it and one below.
    public static ArrayMesh Bipyramid(int sides, float radius, float upperHeight, float lowerHeight)
    {
        SurfaceTool surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        // No shared smoothing group, so every facet stays sharp.
        surface.SetSmoothGroup(uint.MaxValue);

        Vector3 top = new Vector3(0f, upperHeight, 0f);
        Vector3 bottom = new Vector3(0f, -lowerHeight, 0f);

        for (int i = 0; i < sides; i++)
        {
            Vector3 current = RingPoint(i, sides, radius);
            Vector3 next = RingPoint(i + 1, sides, radius);

            // Godot treats clockwise triangles as front-facing.
            surface.AddVertex(top);
            surface.AddVertex(current);
            surface.AddVertex(next);

            surface.AddVertex(bottom);
            surface.AddVertex(next);
            surface.AddVertex(current);
        }

        surface.GenerateNormals();
        return surface.Commit();
    }

    private static Vector3 RingPoint(int index, int sides, float radius)
    {
        float angle = Mathf.Tau * index / sides;
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }
}
