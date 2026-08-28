using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class TagSigilGenerator
{
    public static readonly Tag[] AllTags = (Tag[])Enum.GetValues(typeof(Tag));
    public static int AnchorCount => AllTags.Length;

    public static Vector2 GetAnchorPosition(Tag tag, Vector2 center, float radius)
    {
        int index = Array.IndexOf(AllTags, tag);
        float angleDegrees = -90f + index * (360f / AnchorCount);
        float angleRadians = Mathf.DegToRad(angleDegrees);
        return center + new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians)) * radius;
    }

    public static Vector2[] AngularOrder(IEnumerable<Tag> activeTags, Vector2 center, float radius)
    {
        return activeTags
            .OrderBy(tag => Array.IndexOf(AllTags, tag))
            .Select(tag => GetAnchorPosition(tag, center, radius))
            .ToArray();
    }

    public static Vector2[] StarOrder(IEnumerable<Tag> activeTags, Vector2 center, float radius)
    {
        List<Tag> sorted = activeTags.OrderBy(tag => Array.IndexOf(AllTags, tag)).ToList();
        List<Tag> starOrder = new List<Tag>();

        for (int i = 0; i < sorted.Count; i += 2)
        {
            starOrder.Add(sorted[i]);
        }
        for (int i = 1; i < sorted.Count; i += 2)
        {
            starOrder.Add(sorted[i]);
        }

        return starOrder.Select(tag => GetAnchorPosition(tag, center, radius)).ToArray();
    }

    public static Vector2[] ActivationOrder(IEnumerable<Tag> activeTagsInClickOrder, Vector2 center, float radius)
    {
        return activeTagsInClickOrder
            .Select(tag => GetAnchorPosition(tag, center, radius))
            .ToArray();
    }

    public static Vector2[] SeededShuffleOrder(IEnumerable<Tag> activeTags, Vector2 center, float radius)
    {
        List<Tag> shuffled = activeTags.OrderBy(tag => Array.IndexOf(AllTags, tag)).ToList();

        uint seed = 17;
        foreach (Tag tag in shuffled)
        {
            seed = seed * 31 + (uint)tag;
        }

        RandomNumberGenerator rng = new RandomNumberGenerator();
        rng.Seed = seed;

        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = (int)(rng.Randf() * (i + 1));
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled.Select(tag => GetAnchorPosition(tag, center, radius)).ToArray();
    }
}
