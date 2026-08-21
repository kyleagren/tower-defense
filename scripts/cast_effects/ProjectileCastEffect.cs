using Godot;

public partial class ProjectileCastEffect : Node
{
    [Export] public PackedScene ProjectileScene { get; private set; }
    [Export] public float SpreadAngleDegrees { get; private set; } = 15f;
    [Export] public ProjectileHitEffect HitEffect { get; private set; }

    public void Cast(ProjectileCastContext context, Node3D spawnPoint)
    {
        int count = context.ProjectileCount;

        for (int i = 0; i < count; i++)
        {
            float angleOffset = context.IsNova
                ? 360f / count * i
                : ComputeConeAngle(i, count);

            Projectile projectile = ProjectileScene.Instantiate<Projectile>();
            projectile.Initialize(context);
            projectile.HitEffect = HitEffect;
            spawnPoint.GetTree().CurrentScene.AddChild(projectile);

            projectile.GlobalTransform = spawnPoint.GlobalTransform;
            projectile.RotateY(Mathf.DegToRad(angleOffset));
        }
    }

    private float ComputeConeAngle(int index, int count)
    {
        float totalSpread = SpreadAngleDegrees * (count - 1);
        float startAngle = -totalSpread / 2f;
        return startAngle + (SpreadAngleDegrees * index);
    }
}