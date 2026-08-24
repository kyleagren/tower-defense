using Godot;
using System.Collections.Generic;

public abstract partial class Rune : Resource
{
    public abstract string Name { get; }
    public abstract IReadOnlyList<Tag> RequiredTags { get; }
    public abstract string Description { get; }

    public abstract void Apply(SpellCastContext context);
}
