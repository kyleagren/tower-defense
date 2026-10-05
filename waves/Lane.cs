using Godot;
using System.Collections.Generic;

// A spawner's predicted route: the cheapest navmesh path from it to its target, computed once. The
// route preview draws it and fresh enemies face down it; enemies themselves path on their own.
public class Lane
{
    public IReadOnlyList<Vector3> Waypoints { get; }

    public Lane(Vector3[] waypoints)
    {
        Waypoints = waypoints;
    }
}
