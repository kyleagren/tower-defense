using System;
using System.Collections.Generic;

// The rune-side vocabulary of Tag: which values are rune wheel buckets, and which only a rune may
// carry. Tag is shared with spells so a cross-cutting effect can name "Projectile" once for both.
public static class RuneTags
{
    // The rune wheel's first ring, in ring order clockwise from the top. A rune shows in the bucket
    // of every one of these it carries among its own Tags.
    private static readonly Tag[] _buckets =
    {
        Tag.Trajectory, Tag.AoE, Tag.Targeting, Tag.Cooldown, Tag.Reaction, Tag.Trigger, Tag.Tradeoff, Tag.Utility
    };

    // A bucket's own buckets, shown first in its rune ring. A rune carrying one shows only inside it,
    // not loose in the parent's ring.
    private static readonly Dictionary<Tag, Tag[]> _subBuckets = new()
    {
        { Tag.Targeting, new[] { Tag.TargetStatus } }
    };

    // Describe what kind of rune something is. A spell carrying one, or a rune requiring one, would
    // make that rune incompatible with everything - silently absent from every wheel.
    private static readonly Tag[] _runeOnly = { Tag.Trajectory, Tag.Cooldown, Tag.Reaction, Tag.Tradeoff, Tag.Utility, Tag.Targeting, Tag.TargetStatus };

    public static IReadOnlyList<Tag> Buckets => _buckets;

    public static bool IsBucket(Tag tag) => Array.IndexOf(_buckets, tag) >= 0;

    public static IReadOnlyList<Tag> SubBucketsOf(Tag bucket) => _subBuckets.TryGetValue(bucket, out Tag[] subBuckets) ? subBuckets : Array.Empty<Tag>();

    // Tags whose wheel label isn't their own name: the Area bucket reuses Tag.AoE rather than a
    // near-duplicate tag, and TargetStatus already sits inside Targeting.
    public static string BucketName(Tag bucket) => bucket switch
    {
        Tag.AoE => "Area",
        Tag.TargetStatus => "Status",
        _ => bucket.ToString()
    };
    public static bool IsRuneOnly(Tag tag) => Array.IndexOf(_runeOnly, tag) >= 0;

    public static void Validate(Rune rune)
    {
        bool hasBucket = false;

        foreach (Tag tag in rune.Tags)
        {
            hasBucket |= IsBucket(tag);
        }

        if (!hasBucket)
        {
            throw new InvalidOperationException($"Rune '{rune.Name}' carries no bucket tag, so no rune wheel bucket would show it.");
        }

        ThrowIfRuneOnly(rune.RequiredTags, $"Rune '{rune.Name}' requires");
        ThrowIfRuneOnly(rune.RequiredAnyTags, $"Rune '{rune.Name}' requires");
    }

    public static void Validate(Spell spell)
    {
        ThrowIfRuneOnly(spell.Tags, $"Spell '{spell.Name}' carries");
    }

    private static void ThrowIfRuneOnly(IReadOnlyList<Tag> tags, string subject)
    {
        foreach (Tag tag in tags)
        {
            if (IsRuneOnly(tag))
            {
                throw new InvalidOperationException($"{subject} the rune-only tag {tag}.");
            }
        }
    }
}
