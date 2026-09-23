using Godot;

public class ProjectileCastData
{
    public float Radius;
    public int ProjectileCount = 1;
    public int PierceCount;
    public bool IsNova;
    public bool IsReturn;
    public ScaledStat Speed = 0f;
    public float SpreadAngleDegrees = 15f;
    public PackedScene ProjectileScene;
    public ProjectileHitEffect HitEffect;
}
