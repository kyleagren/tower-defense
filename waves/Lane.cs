using Godot;
using System.Collections.Generic;

// A shared, one-time-computed route from a spawner to its target. Every enemy spawned from the
// same EnemySpawner walks this same Lane by index, instead of each running its own live
// NavigationAgent3D query for an identical path.
public class Lane
{
    public IReadOnlyList<Vector3> Waypoints { get; }

    private readonly float[] _remainingDistance;

    public Lane(Vector3[] waypoints)
    {
        Waypoints = waypoints;
        _remainingDistance = new float[waypoints.Length];

        for (int i = waypoints.Length - 2; i >= 0; i--)
        {
            _remainingDistance[i] = _remainingDistance[i + 1] + waypoints[i].DistanceTo(waypoints[i + 1]);
        }
    }

    public float DistanceToGoal(int waypointIndex, Vector3 currentPosition)
    {
        return waypointIndex >= Waypoints.Count
            ? 0f
            : currentPosition.DistanceTo(Waypoints[waypointIndex]) + _remainingDistance[waypointIndex];
    }
}
