using Godot;
using System;
using System.Collections.Generic;

public static class EnemyTargeting
{
    // Picks a ritual's cast target. Range is always measured from the ritual; only which of the
    // in-range enemies wins depends on the priority.
    // sightFrom, when given, also requires a clear line from there to the enemy (LineOfSight).
    public static Enemy FindTarget(Vector3 fromPosition, float range, TargetPriority priority, ISet<Enemy> exclude = null, Func<Enemy, bool> predicate = null, Vector3? sightFrom = null)
    {
        float RangeSquared(Enemy enemy) => fromPosition.DistanceSquaredTo(enemy.GlobalPosition);

        Func<Enemy, float> score = priority == TargetPriority.ClosestToGoal
            ? enemy => enemy.DistanceToGoal
            : RangeSquared;

        return FindBest(RangeSquared, range, score, exclude, predicate, sightFrom: sightFrom);
    }

    public static Enemy FindNearest(Vector3 fromPosition, float range, ISet<Enemy> exclude = null, Func<Enemy, bool> predicate = null, Vector3? sightFrom = null)
    {
        float DistanceSquared(Enemy enemy) => fromPosition.DistanceSquaredTo(enemy.GlobalPosition);

        return FindBest(DistanceSquared, range, DistanceSquared, exclude, predicate, sightFrom: sightFrom);
    }

    // Two exclusion sets instead of one merged set. A chaining projectile has both its own hit list
    // and the shared per-cast tracker to skip; merging them meant allocating a HashSet on every
    // single chain jump purely to ask a membership question.
    public static Enemy FindNearest(Vector3 fromPosition, float range, ISet<Enemy> exclude, ISet<Enemy> alsoExclude, Vector3? sightFrom = null)
    {
        float DistanceSquared(Enemy enemy) => fromPosition.DistanceSquaredTo(enemy.GlobalPosition);

        return FindBest(DistanceSquared, range, DistanceSquared, exclude, null, alsoExclude, sightFrom);
    }

    // An enemy within range of fromPosition, chosen by how close it is to point - a Resonant Ritual
    // reaching for whatever is nearest its source's target.
    public static Enemy FindNearestTo(Vector3 point, Vector3 fromPosition, float range, Vector3? sightFrom = null)
    {
        float RangeSquared(Enemy enemy) => fromPosition.DistanceSquaredTo(enemy.GlobalPosition);
        float Closeness(Enemy enemy) => point.DistanceSquaredTo(enemy.GlobalPosition);

        return FindBest(RangeSquared, range, Closeness, null, null, sightFrom: sightFrom);
    }

    public static Enemy FindNearestToSegment(Vector3 segmentStart, Vector3 segmentEnd, float range, ISet<Enemy> exclude = null, Func<Enemy, bool> predicate = null)
    {
        float DistanceSquared(Enemy enemy) => GeometryUtils.DistanceSquaredToSegment(enemy.GlobalPosition, segmentStart, segmentEnd);

        return FindBest(DistanceSquared, range, DistanceSquared, exclude, predicate);
    }

    // Every live enemy within radius of origin, measured flat. A new list, collected up front: a kill
    // unregisters from the list being walked, so a caller that deals damage can't walk that one.
    public static List<Enemy> FindAllWithinFlat(Vector3 origin, float radius, Func<Enemy, bool> predicate = null)
    {
        IReadOnlyList<Enemy> candidates = LevelContext.Current.Enemies.Active;
        float radiusSquared = radius * radius;
        List<Enemy> found = new List<Enemy>();

        for (int i = 0; i < candidates.Count; i++)
        {
            Enemy enemy = candidates[i];

            if (GeometryUtils.FlatDistanceSquared(enemy.GlobalPosition, origin) <= radiusSquared
                && (predicate == null || predicate(enemy)))
            {
                found.Add(enemy);
            }
        }

        return found;
    }

    // rangeSquaredTo gates who is eligible; score decides which eligible enemy wins (lowest). The
    // two are separate so a target priority can rank by something other than proximity.
    //
    // exclude is an ISet rather than an IEnumerable so the membership test is a hash lookup per
    // candidate. Passing a lazy sequence (a Concat or an OfType) would silently degrade this to a
    // full re-scan for every enemy considered.
    private static Enemy FindBest(Func<Enemy, float> rangeSquaredTo, float range, Func<Enemy, float> score, ISet<Enemy> exclude, Func<Enemy, bool> predicate, ISet<Enemy> alsoExclude = null, Vector3? sightFrom = null)
    {
        IReadOnlyList<Enemy> candidates = LevelContext.Current.Enemies.Active;
        float rangeSquared = range * range;

        Enemy best = null;
        float bestScore = float.PositiveInfinity;

        // Indexed loop rather than foreach: iterating IReadOnlyList<T> with foreach boxes the
        // enumerator, and this runs per ritual per search interval plus once per chain jump.
        for (int i = 0; i < candidates.Count; i++)
        {
            Enemy enemy = candidates[i];

            if (exclude != null && exclude.Contains(enemy))
            {
                continue;
            }

            if (alsoExclude != null && alsoExclude.Contains(enemy))
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

            if (candidateScore >= bestScore)
            {
                continue;
            }

            // Last, and only for a candidate that would win - it's the one check that costs a raycast.
            if (sightFrom.HasValue && !LineOfSight.IsClear(enemy, sightFrom.Value, enemy.AimPoint))
            {
                continue;
            }

            bestScore = candidateScore;
            best = enemy;
        }

        return best;
    }
}
