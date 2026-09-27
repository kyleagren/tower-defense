using Godot;
using System.Collections.Generic;

public class ProjectileCastEffect : CastEffect
{
    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        ProjectileCastData projectileData = context.Projectile;
        int count = projectileData.ProjectileCount;
        HashSet<Enemy> returnHitTracker = new HashSet<Enemy>();
        HashSet<Enemy> chainHitTracker = new HashSet<Enemy>();

        for (int i = 0; i < count; i++)
        {
            float angleOffset = projectileData.IsNova
                ? 360f / count * i
                : ComputeConeAngle(i, count, projectileData.SpreadAngleDegrees);

            Projectile projectile = projectileData.ProjectileScene.Instantiate<Projectile>();
            projectile.Initialize(context, returnHitTracker, chainHitTracker);
            LevelContext.Current.LevelRoot.AddChild(projectile);

            projectile.GlobalTransform = spawnPoint.GlobalTransform;
            projectile.RotateY(Mathf.DegToRad(angleOffset));
            projectile.SpawnPosition = projectile.GlobalPosition;
        }
    }

    private static float ComputeConeAngle(int index, int count, float spreadAngleDegrees)
    {
        float totalSpread = spreadAngleDegrees * (count - 1);
        float startAngle = -totalSpread / 2f;
        return startAngle + (spreadAngleDegrees * index);
    }
}
