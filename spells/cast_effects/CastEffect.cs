using Godot;

public abstract partial class CastEffect : Node
{
    [Signal] public delegate void ConcludedEventHandler();

    public virtual bool IsOngoing => false;

    public abstract bool CanHandle(SpellCastContext context);
    public abstract void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster);
}
