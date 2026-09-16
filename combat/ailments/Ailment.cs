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
}
