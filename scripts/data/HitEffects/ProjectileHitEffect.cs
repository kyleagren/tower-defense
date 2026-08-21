using Godot;

public abstract partial class ProjectileHitEffect : Resource
{
    public abstract void OnHit(Projectile projectile, Node3D body);
}
