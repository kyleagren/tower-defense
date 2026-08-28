using Godot;

public class ProjectileCastData
{
    public float Radius;
    public int ProjectileCount = 1;
    public int PierceCount;
    public bool IsNova;
    public bool IsReturn;
    public float Speed;
    public float IncreasedSpeedPercent;
    public float MoreSpeedMultiplier = 1f;
    public PackedScene ProjectileScene;
    public ProjectileHitEffect HitEffect;
}
