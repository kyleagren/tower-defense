using Godot;
using System.Collections.Generic;
using System.Linq;

public abstract partial class Spell : Resource
{
    public abstract IReadOnlyList<Tag> Tags { get; }

    private List<Rune> attachedRunes = new List<Rune>();
    public IReadOnlyList<Rune> AttachedRunes => attachedRunes;

    protected abstract SpellCastContext CreateBaseContext();

    public SpellCastContext Resolve()
    {
        SpellCastContext context = CreateBaseContext();

        foreach (Rune rune in attachedRunes)
        {
            rune.Apply(context);
        }

        return context;
    }

    public bool TryAttachRune(Rune rune)
    {
        bool hasRequiredTags = rune.RequiredTags.All(tag => Tags.Contains(tag));
        if (!hasRequiredTags)
        {
            return false;
        }

        bool alreadyAttached = attachedRunes.Any(r => r.GetType() == rune.GetType());
        if (alreadyAttached)
        {
            return false;
        }

        attachedRunes.Add(rune);
        return true;
    }

    public bool DetachRune(Rune rune)
    {
        return attachedRunes.Remove(rune);
    }
}