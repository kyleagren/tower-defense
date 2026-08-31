using Godot;
using System;
using System.Collections.Generic;

public static class EnemyTargeting
{
    // Picks a ritual's cast target. Range is always measured from the ritual; only which of the
    // in-range enemies wins depends on the priority.
    public static Enemy FindTarget(Vector3 fromPosition, float range, TargetPriority priority, ISet<Enemy> exclude = null, Func<Enemy, bool> predicate = null)
    {
        float RangeSquared(Enemy enemy) => fromPosition.DistanceSquaredTo(enemy.GlobalPosition);

        Func<Enemy, float> score = priority == TargetPriority.ClosestToGoal
            ? enemy => enemy.DistanceToGoal
            : RangeSquared;

        return FindBest(RangeSquared, range, score, exclude, predicate);
    }

    public static Enemy FindNearest(Vector3 fromPosition, float range, ISet<Enemy> exclude = null, Func<Enemy, bool> predicate = null)
    {
        float DistanceSquared(Enemy enemy) => fromPosition.DistanceSquaredTo(enemy.GlobalPosition);

        return FindBest(DistanceSquared, range, DistanceSquared, exclude, predicate);
    }

    public static Enemy FindNearestToSegment(Vector3 segmentStart, Vector3 segmentEnd, float range, ISet<Enemy> exclude = null, Func<Enemy, bool> predicate = null)
    {
        float DistanceSquared(Enemy enemy) => GeometryUtils.DistanceSquaredToSegment(enemy.GlobalPosition, segmentStart, segmentEnd);

        return FindBest(DistanceSquared, range, DistanceSquared, exclude, predicate);
    }

    // rangeSquaredTo gates who is eligible; score decides which eligible enemy wins (lowest). The
    // two are separate so a target priority can rank by something other than proximity.
    //
    // exclude is an ISet rather than an IEnumerable so the membership test is a hash lookup per
    // candidate. Passing a lazy sequence (a Concat or an OfType) would silently degrade this to a
    // full re-scan for every enemy considered, which is what it used to do.
    private static Enemy FindBest(Func<Enemy, float> rangeSquaredTo, float range, Func<Enemy, float> score, ISet<Enemy> exclude, Func<Enemy, bool> predicate)
    {
        IReadOnlyList<Enemy> candidates = EnemyRegistry.Active;
        float rangeSquared = range * range;

        Enemy best = null;
        float bestScore = float.MaxValue;

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

            if (rangeSquaredTo(enemy) > rangeSquared)
            {
                continue;
            }

            float candidateScore = score(enemy);
            if (candidateScore <= bestScore)
            {
                bestScore = candidateScore;
                best = enemy;
            }
        }

        return best;
    }
}
