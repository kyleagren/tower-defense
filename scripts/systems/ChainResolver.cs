using Godot;
using System.Collections.Generic;

public static class ChainResolver
{
    public static void ResolveChain(Enemy firstTarget, float damage, float knockbackForce, int chainCount, float chainRange, PackedScene arcVisualScene = null)
    {
        Enemy current = firstTarget;
        HashSet<Enemy> hit = new HashSet<Enemy> { firstTarget };

        for (int i = 0; i < chainCount; i++)
        {
            Enemy next = EnemyTargeting.FindNearest(current.GlobalPosition, chainRange, hit);
            if (next == null)
            {
                break;
            }

            Vector3 knockback = (next.GlobalPosition - current.GlobalPosition).Normalized() * knockbackForce;
            next.TakeDamage(damage, knockback);
            SpawnArcVisual(arcVisualScene, current.GlobalPosition, next.GlobalPosition);

            hit.Add(next);
            current = next;
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
