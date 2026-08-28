using Godot;
using System.Collections.Generic;

public partial class InstantHitCastEffect : CastEffect
{
    [Export] public PackedScene ArcVisualScene { get; private set; }
    [Export] public float ForkPointRatio { get; private set; } = 0.3f;

    public override bool CanHandle(SpellCastContext context) => context.InstantHit != null;

    public override void Cast(SpellCastContext context, Node3D spawnPoint)
    {
        ChainCastData chain = context.Chain ?? new ChainCastData();

        HashSet<Enemy> initialTargets = new HashSet<Enemy>();
        List<Enemy> targets = new List<Enemy>();

        Enemy primary = EnemyTargeting.FindNearest(spawnPoint.GlobalPosition, context.Range, initialTargets);
        if (primary == null)
        {
            return;
        }

        initialTargets.Add(primary);
        targets.Add(primary);

        Vector3 referenceDir = primary.GlobalPosition - spawnPoint.GlobalPosition;

        for (int i = 1; i <= chain.ForkCount; i++)
        {
            float desiredSide = (i % 2 == 1) ? 1f : -1f;

            Enemy target = EnemyTargeting.FindNearest(spawnPoint.GlobalPosition, context.Range, initialTargets,
                candidate => SideOf(spawnPoint.GlobalPosition, referenceDir, candidate.GlobalPosition) * desiredSide > 0f);
            target ??= EnemyTargeting.FindNearest(spawnPoint.GlobalPosition, context.Range, initialTargets);

            if (target == null)
            {
                break;
            }

            initialTargets.Add(target);
            targets.Add(target);
        }

        Vector3 visualOrigin = spawnPoint.GlobalPosition;

        if (targets.Count > 1)
        {
            Vector3 forkPoint = spawnPoint.GlobalPosition.Lerp(targets[0].GlobalPosition, ForkPointRatio);
            SpawnArcVisual(spawnPoint.GlobalPosition, forkPoint);
            visualOrigin = forkPoint;
        }

        HashSet<Enemy> chainTracker = new HashSet<Enemy>(targets);

        foreach (Enemy target in targets)
        {
            StrikeTarget(context, chain, spawnPoint, visualOrigin, target, chainTracker);
        }
    }

    private void StrikeTarget(SpellCastContext context, ChainCastData chain, Node3D spawnPoint, Vector3 visualOrigin, Enemy target, HashSet<Enemy> chainTracker)
    {
        Vector3 targetPoint = target.GetJitteredCenterMass(0.25f);
        Vector3 ragdollImpulse = (targetPoint - spawnPoint.GlobalPosition).Normalized() * context.RagdollForce;
        DamagePacket firstHitDamage = context.Damage * Mathf.Pow(1f + chain.MoreDamagePerRemainingChain, chain.ChainCount);
        target.TakeDamage(firstHitDamage, ragdollImpulse);

        SpawnArcVisual(visualOrigin, targetPoint);

        ChainResolver.ResolveChain(target, targetPoint, context.Damage, context.RagdollForce, chain.ChainCount, chain.ChainRange, chain.MoreDamagePerRemainingChain, ArcVisualScene, chainTracker);
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

    private static float SideOf(Vector3 origin, Vector3 referenceDir, Vector3 point)
    {
        Vector3 toPoint = point - origin;
        return referenceDir.X * toPoint.Z - referenceDir.Z * toPoint.X;
    }
}
