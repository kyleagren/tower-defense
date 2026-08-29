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

    private static Vector2 GridPosition(int index, Vector2 center, float radius)
    {
        int columns = Mathf.CeilToInt(Mathf.Sqrt(AnchorCount));
        int rows = Mathf.CeilToInt((float)AnchorCount / columns);

        int col = index % columns;
        int row = index / columns;

        float x = columns > 1 ? Mathf.Lerp(-radius, radius, (float)col / (columns - 1)) : 0f;
        float y = rows > 1 ? Mathf.Lerp(-radius, radius, (float)row / (rows - 1)) : 0f;

        return center + new Vector2(x, y);
    }

    // Builds up to `panelCount` distinct random pairings of the active tags — each active tag
    // connects to one random other active tag. A panel whose pairing duplicates an earlier one
    // (e.g. because there just aren't many distinct layouts possible with only 2-3 active tags)
    // gets a null entry, so the caller can render it blank instead of repeating a pattern.
    public static (List<Tag> Active, int[][] PairingsPerPanel) GenerateUniquePairings(IEnumerable<Tag> activeTags, int panelCount)
    {
        List<Tag> active = activeTags.OrderBy(tag => Array.IndexOf(AllTags, tag)).ToList();
        int[][] pairings = new int[panelCount][];

        if (active.Count < 2)
        {
            return (active, pairings);
        }

        uint baseSeed = 0;
        foreach (Tag tag in active)
        {
            baseSeed = baseSeed * 31 + (uint)tag;
        }

        HashSet<string> seenSignatures = new HashSet<string>();

        for (int panel = 0; panel < panelCount; panel++)
        {
            uint seed = baseSeed + (uint)panel * 104729u;
            int[] targets = GeneratePairingTargets(active.Count, seed);

            pairings[panel] = seenSignatures.Add(PairingSignature(targets)) ? targets : null;
        }

        return (active, pairings);
    }

    private static int[] GeneratePairingTargets(int count, uint seed)
    {
        RandomNumberGenerator rng = new RandomNumberGenerator { Seed = seed };
        int[] targets = new int[count];

        for (int i = 0; i < count; i++)
        {
            int j = i;
            while (j == i)
            {
                j = rng.RandiRange(0, count - 1);
            }
            targets[i] = j;
        }

        return targets;
    }

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
