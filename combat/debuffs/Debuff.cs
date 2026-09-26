using System;

public enum DebuffApplication
{
    Discard,
    Replace
}

// Any temporary condition on an enemy - a slow, a root, a link, a burn - held in a list on the Enemy
// rather than special-cased into it. One that carries an element is an ailment (IsAilment): Burn,
// Chill and the like. That's a property rather than a subclass, since a debuff can be a general
// mechanic and elemental at once - Chill is a slow that happens to be Cold.
public abstract class Debuff
{
    // The elements this debuff belongs to - empty for an ordinary debuff. A hybrid would credit each
    // of its elements evenly. Subclasses return a cached array, since this is read per check.
    public virtual Tag[] Elements => Array.Empty<Tag>();

    public bool IsAilment => Elements.Length > 0;

    public abstract bool Tick(Enemy enemy, float delta);

    // Fired after the host's health has been reduced but before its death check, so a killing blow
    // still reaches anything keyed off being hit. Never fired for a Mirrored hit - damage that is
    // itself a product of this hook - which is what stops a mirrored hit from bouncing back through
    // the other end of a Health Link.
    public virtual void OnHostDamaged(Enemy host, Hit hit)
    {
    }

    // Multiplied into the host's movement speed. Debuffs that don't touch movement leave it at 1;
    // several that do compound.
    public virtual float SpeedMultiplier => 1f;

    // Two debuffs in the same slot can't coexist - applying one resolves against the other rather
    // than sitting beside it. One per type by default; Health Link widens it to one per partner,
    // and a debuff meant to stack freely would return false for everything.
    public virtual bool OccupiesSameSlotAs(Debuff other) => other.GetType() == GetType();

    // Compared when one debuff lands on another in the same slot: the stronger wins outright, so a
    // weak reapplication can never refresh a strong effect's duration.
    protected virtual float Magnitude => 0f;

    public virtual DebuffApplication ResolveAgainst(Debuff existing) =>
        Magnitude >= existing.Magnitude ? DebuffApplication.Replace : DebuffApplication.Discard;
}
