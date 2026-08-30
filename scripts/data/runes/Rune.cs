using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public abstract partial class Rune : Resource
{
    public abstract string Name { get; }
    public abstract IReadOnlyList<Tag> RequiredTags { get; }
    public virtual IReadOnlyList<Tag> RequiredAnyTags => Array.Empty<Tag>();
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
}
