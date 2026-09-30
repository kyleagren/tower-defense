using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public abstract partial class Rune : Resource
{
    [Export] public Rarity Rarity { get; private set; }

    public abstract string Name { get; }
    public abstract IReadOnlyList<Tag> RequiredTags { get; }
    public virtual IReadOnlyList<Tag> RequiredAnyTags => Array.Empty<Tag>();

    // What kind of rune this is, as opposed to what it requires of a spell. Decides which rune wheel
    // buckets show it (RuneTags.Buckets), and gives items something to name a family of runes by.
    public abstract IReadOnlyList<Tag> Tags { get; }
    public abstract string Description { get; }
    public virtual float ManaCostMultiplier { get; protected set; } = 1.2f;

    // Applied automatically for every attached rune during resolve, same as ManaCostMultiplier —
    // a rune only overrides this if it wants a damage penalty for what it grants.
    public virtual float DamageMultiplier { get; protected set; } = 1f;

    public abstract void Apply(SpellCastContext context);

    public bool IsCompatibleWith(IReadOnlyList<Tag> spellTags)
    {
        bool hasRequiredTags = RequiredTags.All(tag => spellTags.Contains(tag));
        bool hasRequiredAnyTag = RequiredAnyTags.Count == 0 || RequiredAnyTags.Any(tag => spellTags.Contains(tag));
        return hasRequiredTags && hasRequiredAnyTag;
    }

    // Runes sharing a group can't share a Ritual. Each type is its own group by default, which is
    // the one-per-type rule; a family that can only field one member overrides this with a shared key.
    public virtual Type ExclusiveGroup => GetType();

    public bool ConflictsWith(Rune other) => other.ExclusiveGroup == ExclusiveGroup;
}
