using Godot;
using System.Collections.Generic;

public partial class ProjectileCastEffect : CastEffect
{
    [Export] public float SpreadAngleDegrees { get; private set; } = 15f;

    public override void Cast(SpellCastContext context, Node3D spawnPoint)
    {
        if (context is not ProjectileCastContext projectileContext)
        {
            return;
        }

        int count = projectileContext.ProjectileCount;
        HashSet<Enemy> returnHitTracker = new HashSet<Enemy>();
        HashSet<Enemy> chainHitTracker = new HashSet<Enemy>();

        for (int i = 0; i < count; i++)
        {
            float angleOffset = projectileContext.IsNova
                ? 360f / count * i
                : ComputeConeAngle(i, count);

            Projectile projectile = projectileContext.ProjectileScene.Instantiate<Projectile>();
            projectile.Initialize(projectileContext);
            projectile.HitEffect = projectileContext.HitEffect;
            projectile.ReturnHitTracker = returnHitTracker;
            projectile.ChainHitTracker = chainHitTracker;
            spawnPoint.GetTree().CurrentScene.AddChild(projectile);

            projectile.GlobalTransform = spawnPoint.GlobalTransform;
            projectile.RotateY(Mathf.DegToRad(angleOffset));
            projectile.SpawnPosition = projectile.GlobalPosition;
        }
    }

    private float ComputeConeAngle(int index, int count)
    {
        float totalSpread = SpreadAngleDegrees * (count - 1);
        float startAngle = -totalSpread / 2f;
        return startAngle + (SpreadAngleDegrees * index);
    }
}
