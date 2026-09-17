using Godot;
using System.Collections.Generic;

public static class ChainResolver
{
    public const float DamageFalloffPerChain = 0.9f;

    public static void ResolveChain(Enemy firstTarget, Vector3 firstTargetPoint, DamagePacket damage, float ragdollForce, int chainCount, float chainRange, ChainDamageModifier damageModifier = null, SpellHit spellHit = default, PackedScene arcVisualScene = null, HashSet<Enemy> sharedChainTracker = null)
    {
        Enemy current = firstTarget;
        Vector3 currentPoint = firstTargetPoint;

        // What this chain has hit.
        HashSet<Enemy> ownHits = new HashSet<Enemy> { firstTarget };

        // ownHits plus whatever sibling forks/projectiles have hit, built once as a real set
        // rather than re-concatenated lazily on every jump.
        HashSet<Enemy> allHits = sharedChainTracker != null
            ? new HashSet<Enemy>(sharedChainTracker) { firstTarget }
            : ownHits;

        for (int i = 0; i < chainCount; i++)
        {
            Enemy next = EnemyTargeting.FindNearest(current.GlobalPosition, chainRange, allHits);

            // Nothing untouched in range — allow re-hitting something a sibling chain already
            // caught rather than dropping the remaining jumps entirely.
            if (next == null && sharedChainTracker != null)
            {
                next = EnemyTargeting.FindNearest(current.GlobalPosition, chainRange, ownHits);
            }

            if (next == null)
            {
                break;
            }

            int remainingChains = chainCount - (i + 1);
            float multiplier = damageModifier?.GetMultiplier(remainingChains) ?? 1f;
            DamagePacket currentDamage = damage * Mathf.Pow(DamageFalloffPerChain, i + 1) * multiplier;

            Vector3 nextPoint = next.GetJitteredCenterMass(0.25f);
            Vector3 ragdollImpulse = (nextPoint - currentPoint).Normalized() * ragdollForce;
            next.TakeDamage(currentDamage, ragdollImpulse, spellHit: spellHit);
            SpawnArcVisual(next, arcVisualScene, currentPoint, nextPoint);

            ownHits.Add(next);
            allHits.Add(next);
            sharedChainTracker?.Add(next);
            current = next;
            currentPoint = nextPoint;
        }
    }

    // context is any node already in the tree - the caster's spawn point, or the enemy being struck.
    // Taking one avoids Engine.GetMainLoop(), which would reach around the scene tree for something
    // every caller already has in hand.
    public static void SpawnArcVisual(Node context, PackedScene arcVisualScene, Vector3 from, Vector3 to)
    {
        if (arcVisualScene == null)
        {
            return;
        }

        ArcVisual visual = arcVisualScene.Instantiate<ArcVisual>();
        context.GetTree().CurrentScene.AddChild(visual);
        visual.Show(from, to);
    }
}
