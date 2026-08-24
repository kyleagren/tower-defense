using Godot;
using System.Collections.Generic;

public static class ChainResolver
{
    public static void ResolveChain(Enemy firstTarget, Vector3 firstTargetPoint, float damage, float knockbackForce, int chainCount, float chainRange, PackedScene arcVisualScene = null)
    {
        Enemy current = firstTarget;
        Vector3 currentPoint = firstTargetPoint;
        HashSet<Enemy> hit = new HashSet<Enemy> { firstTarget };

        for (int i = 0; i < chainCount; i++)
        {
            Enemy next = EnemyTargeting.FindNearest(current.GlobalPosition, chainRange, hit);
            if (next == null)
            {
                break;
            }

            Vector3 nextPoint = next.GetJitteredCenterMass(0.25f);
            Vector3 knockback = (nextPoint - currentPoint).Normalized() * knockbackForce;
            next.TakeDamage(damage, knockback);
            SpawnArcVisual(arcVisualScene, currentPoint, nextPoint);

            hit.Add(next);
            current = next;
            currentPoint = nextPoint;
        }
    }

    private static void SpawnArcVisual(PackedScene arcVisualScene, Vector3 from, Vector3 to)
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
