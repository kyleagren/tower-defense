using Godot;

// How a hit relates to the damage that caused it - which decides whether it sets the target's
// Status and whether the target's debuffs get to react to it.
public enum HitType
{
    // An ordinary hit: sets Status and reaches the target's debuffs.
    Direct,

    // A direct hit that deliberately sets no Status but still reaches debuffs - Overdrive's shots,
    // which would otherwise paint fresh Status for Harvest to take straight back.
    Unmarked,

    // A damage-over-time tick (Burn): reaches debuffs, but never sets Status - the element came from
    // whatever started the effect, not from the tick.
    DamageOverTime,

    // A copy of damage that already landed somewhere else (a Health Link mirror): sets no Status and
    // never reaches debuffs, which is what stops it bouncing back and forth.
    Mirrored
}

// One hit on its way to a target: the damage, who dealt it, and how it resolves. Built at every
// damage site and handed whole to TakeDamage, so a new piece of hit information is one field here
// rather than another optional parameter threaded through every caller.
public struct Hit
{
    public DamagePacket Damage;
    public Attribution Attribution;
    public HitType Type;
    public Vector3 RagdollImpulse;

    // The casting spell's on-hit runes and tags - default for a hit that isn't from a spell.
    public SpellHit Spell;

    public readonly bool SetsStatus => Type == HitType.Direct;
    public readonly bool NotifiesDebuffs => Type != HitType.Mirrored;
}
