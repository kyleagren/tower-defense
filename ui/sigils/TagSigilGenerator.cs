using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class TagSigilGenerator
{
    public static readonly Tag[] AllTags = (Tag[])Enum.GetValues(typeof(Tag));
    public static int AnchorCount => AllTags.Length;

    // Fixed grid slot for a tag — the intersections of a square grid (e.g. 3x3 for 9 tags,
    // like a tic-tac-toe board), sized to fit every possible tag. A tag always owns the same
    // slot regardless of what else is active.
    public static Vector2 GetAnchorPosition(Tag tag, Vector2 center, float radius)
    {
        int index = Array.IndexOf(AllTags, tag);
        return GridPosition(index, center, radius);
    }

    private static int GridColumns => Mathf.CeilToInt(Mathf.Sqrt(AnchorCount));

    private static Vector2 GetGridCoordinates(int index)
    {
        int columns = GridColumns;
        return new Vector2(index % columns, index / columns);
    }

    private static Vector2 GridPosition(int index, Vector2 center, float radius)
    {
        int columns = GridColumns;
        int rows = Mathf.CeilToInt((float)AnchorCount / columns);
        Vector2 coords = GetGridCoordinates(index);

        float x = columns > 1 ? Mathf.Lerp(-radius, radius, coords.X / (columns - 1)) : 0f;
        float y = rows > 1 ? Mathf.Lerp(-radius, radius, coords.Y / (rows - 1)) : 0f;

        return center + new Vector2(x, y);
    }

    // Builds up to `panelCount` distinct pairings of the active tags — each active tag connects
    // to one other active tag, preferring a nearby neighbor and steering around lines it would
    // otherwise cross. A panel whose pairing duplicates an earlier one (e.g. because there just
    // aren't many distinct layouts possible with only 2-3 active tags) gets a null entry, so the
    // caller can render it blank instead of repeating a pattern.
    public static (List<Tag> Active, int[][] PairingsPerPanel) GenerateUniquePairings(IEnumerable<Tag> activeTags, int panelCount)
    {
        List<Tag> active = activeTags.OrderBy(tag => Array.IndexOf(AllTags, tag)).ToList();
        int[][] pairings = new int[panelCount][];

        if (active.Count < 2)
        {
            return (active, pairings);
        }

        Vector2[] gridCoords = active
            .Select(tag => GetGridCoordinates(Array.IndexOf(AllTags, tag)))
            .ToArray();

        uint baseSeed = 0;
        foreach (Tag tag in active)
        {
            baseSeed = baseSeed * 31 + (uint)tag;
        }

        HashSet<string> seenSignatures = new HashSet<string>();

        for (int panel = 0; panel < panelCount; panel++)
        {
            uint seed = baseSeed + (uint)panel * 104729u;
            int[] targets = GenerateNeighborPairing(gridCoords, seed);

            pairings[panel] = seenSignatures.Add(PairingSignature(targets)) ? targets : null;
        }

        return (active, pairings);
    }

    // Each dot connects to a randomly chosen NEARBY dot rather than any dot in the whole active
    // set, and steers away from candidates whose edge would cross a line already placed in this
    // pairing, so the result reads more like a constellation of local connections than a tangle
    // of lines crossing the whole board.
    private static int[] GenerateNeighborPairing(Vector2[] positions, uint seed)
    {
        const int NeighborPoolSize = 3;

        int count = positions.Length;
        RandomNumberGenerator rng = new RandomNumberGenerator { Seed = seed };
        int[] targets = new int[count];
        List<(int From, int To)> placedEdges = new List<(int, int)>();

        int[] order = Enumerable.Range(0, count).ToArray();
        for (int i = order.Length - 1; i > 0; i--)
        {
            int swapIndex = rng.RandiRange(0, i);
            (order[i], order[swapIndex]) = (order[swapIndex], order[i]);
        }

        foreach (int i in order)
        {
            List<int> byDistance = Enumerable.Range(0, count)
                .Where(j => j != i)
                .OrderBy(j => positions[i].DistanceSquaredTo(positions[j]))
                .ToList();

            List<int> nonCrossing = byDistance
                .Where(candidate => !placedEdges.Any(edge =>
                    edge.From != i && edge.To != i && edge.From != candidate && edge.To != candidate &&
                    SegmentsIntersect(positions[i], positions[candidate], positions[edge.From], positions[edge.To])))
                .ToList();

            List<int> pool = nonCrossing.Count > 0 ? nonCrossing : byDistance;
            int chosen = pool[rng.RandiRange(0, Mathf.Min(NeighborPoolSize, pool.Count) - 1)];

            targets[i] = chosen;
            placedEdges.Add((i, chosen));
        }

        return targets;
    }

    private static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        float d1 = Cross(b2 - b1, a1 - b1);
        float d2 = Cross(b2 - b1, a2 - b1);
        float d3 = Cross(a2 - a1, b1 - a1);
        float d4 = Cross(a2 - a1, b2 - a1);

        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
               ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static string PairingSignature(int[] targets)
    {
        SortedSet<(int, int)> edges = new SortedSet<(int, int)>();

        for (int i = 0; i < targets.Length; i++)
        {
            edges.Add((Mathf.Min(i, targets[i]), Mathf.Max(i, targets[i])));
        }

        return string.Join(",", edges.Select(edge => $"{edge.Item1}-{edge.Item2}"));
    }
}
