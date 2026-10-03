using Godot;

public abstract partial class CastTrigger : Resource
{
    public abstract bool ShouldCast(Ritual caster, SpellCastContext context);

    // True for a trigger that only holds back a cast with nothing worth hitting, rather than deciding
    // when one goes off - a trigger rune can still take over the timing.
    public virtual bool AllowsTriggerRunes => false;
}
