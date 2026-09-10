using Godot;

public abstract partial class CastTrigger : Resource
{
    public abstract bool ShouldCast(Ritual caster, SpellCastContext context);
}
