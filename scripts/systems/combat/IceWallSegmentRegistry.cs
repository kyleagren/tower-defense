using System.Collections.Generic;

// Mirrors EnemyRegistry's shape: live segments only, unregistered immediately on Break() rather
// than waiting for the deferred QueueFree() to actually remove the node from the tree.
public static class IceWallSegmentRegistry
{
    private static readonly List<IceWallSegment> _active = new List<IceWallSegment>();

    public static IReadOnlyList<IceWallSegment> Active => _active;

    public static void Register(IceWallSegment segment)
    {
        _active.Add(segment);
    }

    public static void Unregister(IceWallSegment segment)
    {
        _active.Remove(segment);
    }
}
