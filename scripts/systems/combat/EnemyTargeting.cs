using Godot;
using System;
using System.Collections.Generic;

public static class EnemyTargeting
{
    public static Enemy FindNearest(Vector3 fromPosition, float range, ISet<Enemy> exclude = null, Func<Enemy, bool> predicate = null)
    {
        return FindNearestByDistance(enemy => fromPosition.DistanceSquaredTo(enemy.GlobalPosition), range, exclude, predicate);
    }

    public static Enemy FindNearestToSegment(Vector3 segmentStart, Vector3 segmentEnd, float range, ISet<Enemy> exclude = null, Func<Enemy, bool> predicate = null)
    {
        return FindNearestByDistance(enemy => GeometryUtils.DistanceSquaredToSegment(enemy.GlobalPosition, segmentStart, segmentEnd), range, exclude, predicate);
    }

    // exclude is an ISet rather than an IEnumerable so the membership test is a hash lookup per
    // candidate. Passing a lazy sequence (a Concat or an OfType) would silently degrade this to a
    // full re-scan for every enemy considered, which is what it used to do.
    private static Enemy FindNearestByDistance(Func<Enemy, float> distanceSquaredTo, float range, ISet<Enemy> exclude, Func<Enemy, bool> predicate)
    {
        IReadOnlyList<Enemy> candidates = EnemyRegistry.Active;
        float nearestDistSq = range * range;
        Enemy nearest = null;

        // Indexed loop rather than foreach: iterating IReadOnlyList<T> with foreach boxes the
        // enumerator, and this runs per ritual per search interval plus once per chain jump.
        for (int i = 0; i < candidates.Count; i++)
        {
            Enemy enemy = candidates[i];

            if (exclude != null && exclude.Contains(enemy))
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
}
