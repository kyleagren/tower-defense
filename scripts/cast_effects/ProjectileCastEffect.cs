using Godot;
using System.Collections.Generic;

public partial class ProjectileCastEffect : CastEffect
{
    [Export] public float SpreadAngleDegrees { get; private set; } = 15f;

    public override bool CanHandle(SpellCastContext context) => context.Projectile != null;

    public override void Cast(SpellCastContext context, Node3D spawnPoint)
    {
        if (context.Projectile == null)
        {
            return;
        }

        ProjectileCastData projectileData = context.Projectile;
        int count = projectileData.ProjectileCount;
        HashSet<Enemy> returnHitTracker = new HashSet<Enemy>();
        HashSet<Enemy> chainHitTracker = new HashSet<Enemy>();

        for (int i = 0; i < count; i++)
        {
            float angleOffset = projectileData.IsNova
                ? 360f / count * i
                : ComputeConeAngle(i, count);

            Projectile projectile = projectileData.ProjectileScene.Instantiate<Projectile>();
            projectile.Initialize(context);
            projectile.HitEffect = projectileData.HitEffect;
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
