using Godot;
using System.Collections.Generic;
using System.Linq;

public static class ChainResolver
{
    public const float DamageFalloffPerChain = 0.9f;

    public static void ResolveChain(Enemy firstTarget, Vector3 firstTargetPoint, DamagePacket damage, float ragdollForce, int chainCount, float chainRange, ChainDamageModifier damageModifier = null, ReactionRune reaction = null, PackedScene arcVisualScene = null, HashSet<Enemy> sharedChainTracker = null)
    {
        Enemy current = firstTarget;
        Vector3 currentPoint = firstTargetPoint;
        HashSet<Enemy> hit = new HashSet<Enemy> { firstTarget };

        for (int i = 0; i < chainCount; i++)
        {
            Enemy next = null;

            if (sharedChainTracker != null)
            {
                next = EnemyTargeting.FindNearest(current.GlobalPosition, chainRange, hit.Concat(sharedChainTracker));
            }

            next ??= EnemyTargeting.FindNearest(current.GlobalPosition, chainRange, hit);

            if (next == null)
            {
                break;
            }

            int remainingChains = chainCount - (i + 1);
            float multiplier = damageModifier?.GetMultiplier(remainingChains) ?? 1f;
            DamagePacket currentDamage = damage * Mathf.Pow(DamageFalloffPerChain, i + 1) * multiplier;

            Vector3 nextPoint = next.GetJitteredCenterMass(0.25f);
            Vector3 ragdollImpulse = (nextPoint - currentPoint).Normalized() * ragdollForce;
            next.TakeDamage(currentDamage, ragdollImpulse, reaction: reaction);
            SpawnArcVisual(arcVisualScene, currentPoint, nextPoint);

            hit.Add(next);
            sharedChainTracker?.Add(next);
            current = next;
            currentPoint = nextPoint;
        }
    }

    public static void SpawnArcVisual(PackedScene arcVisualScene, Vector3 from, Vector3 to)
    {
        if (arcVisualScene == null)
        {
            return;
        }

        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        ArcVisual visual = arcVisualScene.Instantiate<ArcVisual>();
        tree.CurrentScene.AddChild(visual);
        visual.Show(from, to);
    }
}
