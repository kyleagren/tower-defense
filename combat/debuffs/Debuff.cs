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

    // A hard-CC ailment's window after its effect ends: it still holds its slot, so the same CC can't
    // land again yet, but it does nothing, isn't drawn, and doesn't count as present.
    public virtual bool IsRecovering => false;

    public bool IsAilment => Elements.Length > 0 && !IsRecovering;

    public abstract DebuffType Type { get; }

    public abstract bool Tick(Enemy enemy, float delta);

    // Runs on the enemy the moment this lands, before it's slotted - for a debuff that takes
    // something from the enemy as it arrives.
    public virtual void OnApplying(Enemy host)
    {
    }

    // Sent to the host when applied to a copy, as an array led by the debuff's type. Null for a
    // debuff that only ever starts on the host.
    public virtual Godot.Collections.Array ToNetwork() => null;

    // attribution is the sender's - their seat and their Sentinel on this machine.
    public static Debuff FromNetwork(Godot.Collections.Array data, Attribution attribution) => (DebuffType)data[0].AsInt32() switch
    {
        DebuffType.Burn => BurnAilment.Decode(data, attribution),
        DebuffType.Immolation => ImmolationAilment.Decode(data, attribution),
        DebuffType.Chill => ChillAilment.Decode(data),
        DebuffType.Converged => ConvergedDebuff.Decode(data),
        DebuffType.Taunted => TauntedDebuff.Decode(data, attribution),
        _ => null
    };

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
