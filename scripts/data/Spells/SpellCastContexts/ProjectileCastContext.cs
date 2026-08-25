using Godot;

public class ProjectileCastContext : ChainableSpellCastContext
{
    public float Radius;
    public int ProjectileCount;
    public int PierceCount;
    public bool IsNova;
    public bool IsReturn;
    public PackedScene ProjectileScene;
    public ProjectileHitEffect HitEffect;
}
