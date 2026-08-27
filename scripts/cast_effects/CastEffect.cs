using Godot;

public abstract partial class CastEffect : Node
{
    public abstract bool CanHandle(SpellCastContext context);
    public abstract void Cast(SpellCastContext context, Node3D spawnPoint);
}
