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
        Transform3D launch = projectileData.IsNova ? Leveled(spawnPoint.GlobalTransform) : spawnPoint.GlobalTransform;

        for (int i = 0; i < count; i++)
        {
            float angleOffset = projectileData.IsNova
                ? 360f / count * i
                : ComputeConeAngle(i, count, projectileData.SpreadAngleDegrees);

            Projectile projectile = projectileData.ProjectileScene.Instantiate<Projectile>();
            projectile.Initialize(context, returnHitTracker, chainHitTracker);
            LevelContext.Current.LevelRoot.AddChild(projectile);

            projectile.GlobalTransform = launch;
            projectile.RotateY(Mathf.DegToRad(angleOffset));
            projectile.SpawnPosition = projectile.GlobalPosition;
            LevelContext.Current.SpellEffects.LaunchProjectile(projectile, projectileData.ProjectileScene);
        }
    }

    // A Nova's ring stays horizontal however steeply the Ritual is aiming, rather than tilting
    // into a cone that drives its forward half into the ground.
    private static Transform3D Leveled(Transform3D transform)
    {
        Vector3 forward = -transform.Basis.Z;
        forward.Y = 0f;

        // The Ritual never aims straight up or down (AimSpawnPointAt refuses to), so a flat
        // direction always remains.
        transform.Basis = Basis.LookingAt(forward.Normalized(), Vector3.Up);
        return transform;
    }

    private static float ComputeConeAngle(int index, int count, float spreadAngleDegrees)
    {
        float totalSpread = spreadAngleDegrees * (count - 1);
        float startAngle = -totalSpread / 2f;
        return startAngle + (spreadAngleDegrees * index);
    }
}
