using System.Collections.Generic;

// A flat list of the live instances of one type, maintained by the instances themselves. Replaces
// GetNodesInGroup on hot paths: no per-call Godot Array allocation, no marshalling of Node handles
// across the engine boundary, and no per-element type check.
//
// Membership means "alive and interactable," not "in the tree" - an Enemy leaves the moment it
// dies, so ragdolls waiting on their cleanup timer are never considered as targets, and an
// IceWallSegment leaves on Break() rather than waiting for its deferred QueueFree to land.
//
// Named per-type through the aliases in GlobalUsings.cs, so call sites still read EnemyRegistry /
// IceWallSegmentRegistry rather than a bare generic.
public static class ActiveRegistry<T>
{
    private static readonly List<T> _active = new List<T>();

    public static IReadOnlyList<T> Active => _active;

    public static void Register(T item)
    {
        _active.Add(item);
    }

    public static void Unregister(T item)
    {
        _active.Remove(item);
    }

    // Static state outlives a scene reload, so draining it depends on every instance being freed
    // cleanly through _ExitTree. Cleared explicitly on reload as well, so one leaked entry can't
    // survive into the next match and be handed out as a target.
    public static void Clear()
    {
        _active.Clear();
    }
}
