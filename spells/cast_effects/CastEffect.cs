using Godot;

// How a spell is delivered once its Ritual decides to cast - a projectile, an instant strike, a
// wall, a bombardment. Chosen by the spell's AddComponents and carried on the context, so a
// rune could swap it like any other field. Stateless: everything a cast needs arrives through the
// context, so one instance is safe to share between Rituals.
public abstract class CastEffect
{
    // An ongoing effect keeps its Ritual from recasting until it calls caster.ConcludeOngoingEffect()
    // - the cooldown starts then, not at the cast.
    public virtual bool IsOngoing => false;

    public abstract void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster);
}
