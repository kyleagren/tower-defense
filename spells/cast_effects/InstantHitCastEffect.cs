using Godot;
using System.Collections.Generic;

public class InstantHitCastEffect : CastEffect
{
    public override void Cast(SpellCastContext context, Node3D spawnPoint, Ritual caster)
    {
        ChainCastData chain = context.Chain ?? new ChainCastData();
        List<Enemy> targets = FindTargets(context, chain, spawnPoint);

        if (targets.Count == 0)
        {
            return;
        }

        Vector3 visualOrigin = spawnPoint.GlobalPosition;

        if (targets.Count > 1)
        {
            Vector3 forkPoint = spawnPoint.GlobalPosition.Lerp(targets[0].GlobalPosition, context.InstantHit.ForkPointRatio);
            ChainResolver.SpawnArcVisual(spawnPoint, context.InstantHit.ArcVisualScene, spawnPoint.GlobalPosition, forkPoint);
            visualOrigin = forkPoint;
        }

        HashSet<Enemy> chainTracker = new HashSet<Enemy>(targets);

        foreach (Enemy target in targets)
        {
            StrikeTarget(context, chain, spawnPoint, visualOrigin, target, chainTracker);
        }
    }

    private List<Enemy> FindTargets(SpellCastContext context, ChainCastData chain, Node3D spawnPoint)
    {
        HashSet<Enemy> initialTargets = new HashSet<Enemy>();
        List<Enemy> targets = new List<Enemy>();

        // The primary strike follows the spell's target priority; the forks below deliberately
        // stay nearest-first, since a fork is "an additional nearby enemy at the same time".
        Enemy primary = EnemyTargeting.FindTarget(spawnPoint.GlobalPosition, context.Range.Value, context.TargetPriority, initialTargets);
        if (primary == null)
        {
            return targets;
        }

        initialTargets.Add(primary);
        targets.Add(primary);

        Vector3 referenceDir = primary.GlobalPosition - spawnPoint.GlobalPosition;

        for (int i = 1; i <= chain.ForkCount; i++)
        {
            float desiredSide = (i % 2 == 1) ? 1f : -1f;

            Enemy target = EnemyTargeting.FindNearest(spawnPoint.GlobalPosition, context.Range.Value, initialTargets,
                candidate => SideOf(spawnPoint.GlobalPosition, referenceDir, candidate.GlobalPosition) * desiredSide > 0f);
            target ??= EnemyTargeting.FindNearest(spawnPoint.GlobalPosition, context.Range.Value, initialTargets);

            if (target == null)
            {
                break;
            }

            initialTargets.Add(target);
            targets.Add(target);
        }

        return targets;
    }

    private void StrikeTarget(SpellCastContext context, ChainCastData chain, Node3D spawnPoint, Vector3 visualOrigin, Enemy target, HashSet<Enemy> chainTracker)
    {
        Vector3 targetPoint = target.GetJitteredCenterMass(0.25f);
        Vector3 ragdollImpulse = (targetPoint - spawnPoint.GlobalPosition).Normalized() * context.RagdollForce.Value;
        float firstHitMultiplier = chain.DamageModifier?.GetMultiplier(chain.ChainCount) ?? 1f;
        DamagePacket firstHitDamage = context.Damage * firstHitMultiplier;
        target.TakeDamage(context.SpellHit.CreateHit(firstHitDamage, ragdollImpulse));

        ChainResolver.SpawnArcVisual(spawnPoint, context.InstantHit.ArcVisualScene, visualOrigin, targetPoint);

        ChainResolver.ResolveChain(target, targetPoint, context.Damage, context.RagdollForce.Value, chain.ChainCount, chain.ChainRange, chain.DamageModifier, context.SpellHit, context.InstantHit.ArcVisualScene, chainTracker);
    }

    private static float SideOf(Vector3 origin, Vector3 referenceDir, Vector3 point)
    {
        Vector3 toPoint = point - origin;
        return referenceDir.X * toPoint.Z - referenceDir.Z * toPoint.X;
    }
}
