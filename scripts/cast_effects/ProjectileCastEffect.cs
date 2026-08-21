using Godot;

[GlobalClass]
public partial class ProjectileCastEffect : Node
{
    [Export] private PackedScene projectileScene;

    [Export] private float spreadAngleDegrees = 15f;

    public void Cast(ProjectileCastContext context, Node3D spawnPoint)
    {
        int count = context.ProjectileCount;
        float totalSpread = spreadAngleDegrees * (count - 1);
        float startAngle = -totalSpread / 2f;

        for (int i = 0; i < count; i++)
        {
            float angleOffset = startAngle + (spreadAngleDegrees * i);

            Projectile projectile = projectileScene.Instantiate<Projectile>();
            projectile.Initialize(context);
            spawnPoint.GetTree().CurrentScene.AddChild(projectile);

            projectile.GlobalTransform = spawnPoint.GlobalTransform;
            projectile.RotateY(Mathf.DegToRad(angleOffset));
        }
    }
}