using Godot;
using System;
using System.Collections.Generic;

public abstract partial class Rune : Resource
{
    public abstract string Name { get; }
    public abstract IReadOnlyList<Tag> RequiredTags { get; }
    public virtual IReadOnlyList<Tag> RequiredAnyTags => Array.Empty<Tag>();
    public abstract string Description { get; }
    public virtual float ManaCostMultiplier { get; protected set; } = 1.2f;

    public abstract void Apply(SpellCastContext context);
}
