using Godot;

// Whether terrain stands between two points. Only level geometry blocks sight - Ice Walls,
// Rituals, players and enemies never do.
public static class LineOfSight
{
    // Reused rather than made per call; every caller runs on the main thread during physics.
    private static readonly PhysicsRayQueryParameters3D _query = new() { CollisionMask = PhysicsLayers.World };

    // context is any node in the world being asked about.
    public static bool IsClear(Node3D context, Vector3 from, Vector3 to)
    {
        _query.From = from;
        _query.To = to;
        return context.GetWorld3D().DirectSpaceState.IntersectRay(_query).Count == 0;
    }
}
