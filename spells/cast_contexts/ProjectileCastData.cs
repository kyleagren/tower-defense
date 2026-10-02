using Godot;

public class ProjectileCastData
{
    public float Radius;
    // 0 is a sphere. Above 0, a capsule this long, lying along the flight or, if Crosswise, across it.
    public float Length;
    public bool Crosswise;
    public int ProjectileCount = 1;
    public int PierceCount;
    public bool IsNova;
    public bool IsReturn;
    public ScaledStat Speed = 0f;
    public float SpreadAngleDegrees = 15f;
    public PackedScene ProjectileScene;
    public ProjectileHitEffect HitEffect;
}
