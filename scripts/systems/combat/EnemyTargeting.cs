using Godot;
using System;
using System.Collections.Generic;

public static class EnemyTargeting
{
    public static Enemy FindNearest(Vector3 fromPosition, float range, IEnumerable<Enemy> exclude = null, Func<Enemy, bool> predicate = null)
    {
        return FindNearestByDistance(enemy => fromPosition.DistanceSquaredTo(enemy.GlobalPosition), range, exclude, predicate);
    }

    public static Enemy FindNearestToSegment(Vector3 segmentStart, Vector3 segmentEnd, float range, IEnumerable<Enemy> exclude = null, Func<Enemy, bool> predicate = null)
    {
        return FindNearestByDistance(enemy => GeometryUtils.DistanceSquaredToSegment(enemy.GlobalPosition, segmentStart, segmentEnd), range, exclude, predicate);
    }

    private static Enemy FindNearestByDistance(Func<Enemy, float> distanceSquaredTo, float range, IEnumerable<Enemy> exclude, Func<Enemy, bool> predicate)
    {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        float rangeSquared = range * range;

        Enemy nearest = null;
        float nearestDistSq = rangeSquared;

        foreach (Node node in tree.GetNodesInGroup("enemies"))
        {
            if (node is not Enemy enemy || enemy.IsDead)
            {
                continue;
            }

            if (exclude != null && Contains(exclude, enemy))
            {
                continue;
            }

            if (predicate != null && !predicate(enemy))
            {
                continue;
            }

            float distSq = distanceSquaredTo(enemy);
            if (distSq <= nearestDistSq)
            {
                nearestDistSq = distSq;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private static bool Contains(IEnumerable<Enemy> collection, Enemy value)
    {
        if (collection is ICollection<Enemy> hashCollection)
        {
            return hashCollection.Contains(value);
        }

        foreach (Enemy enemy in collection)
        {
            if (enemy == value) return true;
        }

        return false;
    }
}
