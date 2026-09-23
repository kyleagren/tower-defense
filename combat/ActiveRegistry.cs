using System.Collections.Generic;

// A flat list of the live instances of one type, maintained by the instances themselves. Used
// instead of GetNodesInGroup on hot paths: no per-call Godot Array allocation, no marshalling of
// Node handles across the engine boundary, and no per-element type check.
//
// Membership means "alive and interactable," not "in the tree" - an Enemy leaves the moment it
// dies, so ragdolls waiting on their cleanup timer are never considered as targets, and an
// IceWallSegment leaves on Break() rather than waiting for its deferred QueueFree to land.
public class ActiveRegistry<T>
{
    private readonly List<T> _active = new List<T>();

    public IReadOnlyList<T> Active => _active;

    public void Register(T item)
    {
        _active.Add(item);
    }

    public void Unregister(T item)
    {
        _active.Remove(item);
    }
}
