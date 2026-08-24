using Godot;

public abstract partial class CastEffect : Node
{
    public abstract void Cast(SpellCastContext context, Node3D spawnPoint);
}
