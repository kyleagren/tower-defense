using System;
using System.Collections.Generic;

// Which Tag values are elements. Tag also holds spell shapes and rune categories, so anything that
// treats a Tag as an element asks here instead of keeping its own list.
public static class ElementTags
{
    private static readonly Tag[] _all = { Tag.Fire, Tag.Water, Tag.Cold, Tag.Lightning, Tag.Wind };

    // Wind is consumer-only and never becomes a Status others could consume. Also what Harvest can
    // be fed, since it charges off Statuses.
    private static readonly Tag[] _statusCapable = { Tag.Fire, Tag.Water, Tag.Cold, Tag.Lightning };

    public static IReadOnlyList<Tag> All => _all;
    public static IReadOnlyList<Tag> StatusCapable => _statusCapable;

    public static bool IsElement(Tag tag) => Array.IndexOf(_all, tag) >= 0;
}
