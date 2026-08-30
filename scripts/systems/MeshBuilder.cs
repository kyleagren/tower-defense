using Godot;

public static class MeshBuilder
{
    public static void AddQuad(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        tool.AddVertex(a);
        tool.AddVertex(b);
        tool.AddVertex(c);

        tool.AddVertex(a);
        tool.AddVertex(c);
        tool.AddVertex(d);
    }

    // Four quads stitching an outer rectangle to an inner one, producing a flat border ring.
    // Both arrays run corner-for-corner in the same winding order.
    public static void AddBorderRing(SurfaceTool tool, Vector3[] inner, Vector3[] outer)
    {
        AddQuad(tool, outer[0], outer[1], inner[1], inner[0]);
        AddQuad(tool, outer[3], outer[2], inner[2], inner[3]);
        AddQuad(tool, outer[0], outer[3], inner[3], inner[0]);
        AddQuad(tool, outer[1], outer[2], inner[2], inner[1]);
    }
}
