public enum AilmentApplication
{
    Discard,
    Replace
}

public abstract class Ailment
{
    public abstract bool Tick(Enemy enemy, float delta);

    // Fired after the host's health has been reduced but before its death check, so a killing blow
    // still reaches anything keyed off being hit. Never fired for damage that is itself a product
    // of this hook - see Enemy.TakeDamage's notifyAilments - which is what stops a mirrored hit
    // from bouncing back through the other end of a Health Link.
    public virtual void OnHostDamaged(Enemy host, DamagePacket damage)
    {
    }

    // Multiplied into the host's movement speed. Ailments that don't touch movement leave it at 1;
    // several that do compound.
    public virtual float SpeedMultiplier => 1f;

    // Two ailments in the same slot can't coexist - applying one resolves against the other rather
    // than sitting beside it. One per type by default; Health Link widens it to one per partner,
    // and an ailment meant to stack freely would return false for everything.
    public virtual bool OccupiesSameSlotAs(Ailment other) => other.GetType() == GetType();

    // Compared when one ailment lands on another in the same slot: the stronger wins outright, so a
    // weak reapplication can never refresh a strong effect's duration.
    protected virtual float Magnitude => 0f;

    public virtual AilmentApplication ResolveAgainst(Ailment existing) =>
        Magnitude >= existing.Magnitude ? AilmentApplication.Replace : AilmentApplication.Discard;
}
