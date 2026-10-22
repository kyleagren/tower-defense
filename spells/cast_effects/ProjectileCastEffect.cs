using Godot;
using System.Collections.Generic;

public class ProjectileCastEffect : CastEffect
{
    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        ProjectileCastData projectileData = context.Projectile;
        HashSet<Enemy> returnHitTracker = new HashSet<Enemy>();
        HashSet<Enemy> chainHitTracker = new HashSet<Enemy>();
        Transform3D launch = projectileData.IsNova ? Leveled(spawnPoint.GlobalTransform) : spawnPoint.GlobalTransform;
        List<Projectile> volley = new List<Projectile>();

        foreach (Transform3D start in Layout(launch, projectileData.ProjectileCount, projectileData.Rows, projectileData.SpreadAngleDegrees, projectileData.IsNova))
        {
            Projectile projectile = projectileData.ProjectileScene.Instantiate<Projectile>();
            projectile.Initialize(context, returnHitTracker, chainHitTracker);
            LevelContext.Current.LevelRoot.AddChild(projectile);
            projectile.GlobalTransform = start;
            projectile.SpawnPosition = projectile.GlobalPosition;
            volley.Add(projectile);
        }

        LevelContext.Current.SpellEffects.LaunchVolley(volley, projectileData.ProjectileScene, launch, projectileData);
    }

    // Where each projectile in a volley starts: count columns fanned across (ringed, for a Nova), each
    // a column of rows stacked up. Every machine lays out a volley through here, so each copy lines
    // up with its original from one message.
    public static List<Transform3D> Layout(Transform3D launch, int count, int rows, float spreadAngleDegrees, bool isNova)
    {
        List<Transform3D> layout = new List<Transform3D>();

        for (int i = 0; i < count; i++)
        {
            float yaw = isNova ? 360f / count * i : ComputeConeAngle(i, count, spreadAngleDegrees);

            for (int row = 0; row < rows; row++)
            {
                float pitch = ComputeConeAngle(row, rows, spreadAngleDegrees);
                Transform3D start = launch;
                start.Basis = new Basis(Vector3.Up, Mathf.DegToRad(yaw)) * launch.Basis * new Basis(Vector3.Right, Mathf.DegToRad(pitch));
                layout.Add(start);
            }
        }

        return layout;
    }

    // A Nova's ring stays horizontal however steeply the Ritual is aiming, rather than tilting
    // into a cone that drives its forward half into the ground.
    private static Transform3D Leveled(Transform3D transform)
    {
        Vector3 forward = -transform.Basis.Z;
        forward.Y = 0f;

        // The Ritual never aims straight up or down (RitualTargeting.AimAt refuses to), so a flat
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
