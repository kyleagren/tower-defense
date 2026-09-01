using Godot;

public partial class WallFootprintMarker : StaticBody3D
{
    // A thin ice-colored outline on the ground tracing the wall's own footprint, plus a row of
    // collision boxes standing in for the segments that won't physically exist until the wall
    // actually casts (ProximityCastTrigger needs an enemy nearby - see WallCastEffect). These sit
    // on PlacementBlocked rather than Obstacles specifically so enemies don't collide with a wall
    // that hasn't triggered yet, while ritual placement checks still treat the space as reserved.
    public void Initialize(Vector3 start, Vector3 end, float segmentWidth)
    {
        Vector3 localStart = ToLocal(start);
        Vector3 localEnd = ToLocal(end);

        BuildVisual(localStart, localEnd, segmentWidth);
        BuildCollision(localStart, localEnd, segmentWidth);
    }

    private void BuildVisual(Vector3 localStart, Vector3 localEnd, float segmentWidth)
    {
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

        MeshInstance3D visual = new MeshInstance3D
        {
            Mesh = surfaceTool.Commit(),
            MaterialOverride = PreviewMaterials.WallFootprint
        };

        AddChild(visual);
    }

    private void BuildCollision(Vector3 localStart, Vector3 localEnd, float segmentWidth)
    {
        CollisionLayer = PhysicsLayers.PlacementBlocked;
        CollisionMask = 0;

        int segmentCount = IceWall.ComputeSegmentCount(localStart.DistanceTo(localEnd), segmentWidth);

        for (int i = 0; i < segmentCount; i++)
        {
            float t = segmentCount > 1 ? (float)i / (segmentCount - 1) : 0f;
            Vector3 position = localStart.Lerp(localEnd, t) + Vector3.Up * segmentWidth;

            CollisionShape3D collisionShape = new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(segmentWidth, segmentWidth * 2f, segmentWidth) },
                Position = position
            };

            AddChild(collisionShape);
        }
    }
}
