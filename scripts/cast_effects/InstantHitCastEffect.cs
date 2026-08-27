using Godot;
using System.Collections.Generic;

public partial class InstantHitCastEffect : CastEffect
{
    [Export] public PackedScene ArcVisualScene { get; private set; }
    [Export] public float ForkPointRatio { get; private set; } = 0.3f;

    public override void Cast(SpellCastContext context, Node3D spawnPoint)
    {
        if (context is not InstantHitCastContext instantContext)
        {
            return;
        }

        HashSet<Enemy> initialTargets = new HashSet<Enemy>();
        List<Enemy> targets = new List<Enemy>();

        for (int i = 0; i <= instantContext.ForkCount; i++)
        {
            Enemy target = EnemyTargeting.FindNearest(spawnPoint.GlobalPosition, instantContext.Range, initialTargets);
            if (target == null)
            {
                break;
            }

            initialTargets.Add(target);
            targets.Add(target);
        }

        if (targets.Count == 0)
        {
            return;
        }

        Vector3 visualOrigin = spawnPoint.GlobalPosition;

        if (targets.Count > 1)
        {
            Vector3 forkPoint = spawnPoint.GlobalPosition.Lerp(targets[0].GlobalPosition, ForkPointRatio);
            SpawnArcVisual(spawnPoint.GlobalPosition, forkPoint);
            visualOrigin = forkPoint;
        }

        foreach (Enemy target in targets)
        {
            StrikeTarget(instantContext, spawnPoint, visualOrigin, target);
        }
    }

    private void StrikeTarget(InstantHitCastContext instantContext, Node3D spawnPoint, Vector3 visualOrigin, Enemy target)
    {
        Vector3 targetPoint = target.GetJitteredCenterMass(0.25f);
        Vector3 ragdollImpulse = (targetPoint - spawnPoint.GlobalPosition).Normalized() * instantContext.RagdollForce;
        DamagePacket firstHitDamage = instantContext.Damage * Mathf.Pow(1f + instantContext.MoreDamagePerRemainingChain, instantContext.ChainCount);
        target.TakeDamage(firstHitDamage, ragdollImpulse);

        SpawnArcVisual(visualOrigin, targetPoint);

        ChainResolver.ResolveChain(target, targetPoint, instantContext.Damage, instantContext.RagdollForce, instantContext.ChainCount, instantContext.ChainRange, instantContext.MoreDamagePerRemainingChain, ArcVisualScene);
    }

    private void SpawnArcVisual(Vector3 from, Vector3 to)
    {
        if (ArcVisualScene == null)
        {
            return;
        }

        ArcVisual visual = ArcVisualScene.Instantiate<ArcVisual>();
        GetTree().CurrentScene.AddChild(visual);
        visual.Show(from, to);
    }
}
