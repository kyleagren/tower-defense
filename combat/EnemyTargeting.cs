using Godot;
using System;
using System.Collections.Generic;

public static class EnemyTargeting
{
    // How far around a candidate MostEnemiesNearby counts its neighbours.
    private const float ClusterRadius = 4f;

    // Picks a ritual's cast target. Range is always measured from the ritual; only which of the
    // in-range enemies wins depends on the context's priority.
    // sightFrom, when given, also requires a clear line from there to the enemy (LineOfSight).
    public static Enemy FindTarget(Vector3 fromPosition, SpellCastContext context, ISet<Enemy> exclude = null, Vector3? sightFrom = null)
    {
        float RangeSquared(Enemy enemy) => fromPosition.DistanceSquaredTo(enemy.GlobalPosition);
        float range = context.Range.Value;

        if (context.TargetPriority == TargetPriority.HasStatus)
        {
            Tag? wanted = context.TargetStatus;
            bool Carries(Enemy enemy) => StatusTargetingRune.Carries(enemy, wanted);

            // With nothing in range carrying it, the ritual targets as normal rather than idling.
            return FindBest(RangeSquared, range, ClosestToGoal, exclude, Carries, sightFrom: sightFrom)
                ?? FindBest(RangeSquared, range, ClosestToGoal, exclude, null, sightFrom: sightFrom);
        }

        Func<Enemy, float> score = context.TargetPriority switch
        {
            TargetPriority.ClosestToRitual => RangeSquared,
            TargetPriority.FurthestFromRitual => enemy => -RangeSquared(enemy),
            TargetPriority.HighestMaxHealth => enemy => -enemy.MaxHealth,
            TargetPriority.LowestHealth => enemy => enemy.CurrentHealth,
            TargetPriority.MostEnemiesNearby => enemy => -CountWithinFlat(enemy.GlobalPosition, ClusterRadius),
            TargetPriority.Random => _ => GD.Randf(),
            _ => ClosestToGoal
        };

        // Ties are common - same-type enemies at full health, packs of equal size - and go to the one
        // furthest along.
        return FindBest(RangeSquared, range, score, exclude, null, sightFrom: sightFrom, tiebreak: ClosestToGoal);
    }

    // The count best targets by the context's priority, best first - fewer when fewer are in range.
    public static List<Enemy> FindTargets(Vector3 fromPosition, SpellCastContext context, int count, Vector3? sightFrom = null)
    {
        HashSet<Enemy> chosen = new HashSet<Enemy>();
        List<Enemy> targets = new List<Enemy>();

        while (targets.Count < count)
        {
            Enemy next = FindTarget(fromPosition, context, chosen, sightFrom);

            if (next == null)
            {
                break;
            }

            chosen.Add(next);
            targets.Add(next);
        }

        return targets;
    }

    private static float ClosestToGoal(Enemy enemy) => enemy.DistanceToGoal;

    // Counts the enemy at origin too, which every candidate does alike.
    private static int CountWithinFlat(Vector3 origin, float radius)
    {
        IReadOnlyList<Enemy> enemies = LevelContext.Current.Enemies.Active;
        float radiusSquared = radius * radius;
        int count = 0;

        for (int i = 0; i < enemies.Count; i++)
        {
            if (GeometryUtils.FlatDistanceSquared(enemies[i].GlobalPosition, origin) <= radiusSquared)
            {
                count++;
            }
        }

        return count;
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

    public static Enemy FindNearestToSegment(Vector3 segmentStart, Vector3 segmentEnd, float range, ISet<Enemy> exclude = null, Func<Enemy, bool> predicate = null, Vector3? sightFrom = null)
    {
        float DistanceSquared(Enemy enemy) => GeometryUtils.DistanceSquaredToSegment(enemy.GlobalPosition, segmentStart, segmentEnd);

        return FindBest(DistanceSquared, range, DistanceSquared, exclude, predicate, sightFrom: sightFrom);
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
    private static Enemy FindBest(Func<Enemy, float> rangeSquaredTo, float range, Func<Enemy, float> score, ISet<Enemy> exclude, Func<Enemy, bool> predicate, ISet<Enemy> alsoExclude = null, Vector3? sightFrom = null, Func<Enemy, float> tiebreak = null)
    {
        IReadOnlyList<Enemy> candidates = LevelContext.Current.Enemies.Active;
        float rangeSquared = range * range;

        Enemy best = null;
        float bestScore = float.PositiveInfinity;
        float bestTiebreak = float.PositiveInfinity;

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

            if (candidateScore > bestScore)
            {
                continue;
            }

            float candidateTiebreak = tiebreak?.Invoke(enemy) ?? 0f;

            if (candidateScore == bestScore && (tiebreak == null || candidateTiebreak >= bestTiebreak))
            {
                continue;
            }

            // Last, and only for a candidate that would win - it's the one check that costs a raycast.
            if (sightFrom.HasValue && !LineOfSight.IsClear(enemy, sightFrom.Value, enemy.AimPoint))
            {
                continue;
            }

            bestScore = candidateScore;
            bestTiebreak = candidateTiebreak;
            best = enemy;
        }

        return best;
    }
}
