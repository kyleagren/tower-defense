using Godot;
using System.Collections.Generic;

public static class EnemyTargeting
{
    public static Enemy FindNearest(Vector3 fromPosition, float range, IEnumerable<Enemy> exclude = null)
    {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        float rangeSquared = range * range;

        Enemy nearest = null;
        float nearestDistSq = rangeSquared;

        foreach (Node node in tree.GetNodesInGroup("enemies"))
        {
            if (node is not Enemy enemy || enemy.IsDead)
            {
                continue;
            }

            if (exclude != null && Contains(exclude, enemy))
            {
                continue;
            }

            float distSq = fromPosition.DistanceSquaredTo(enemy.GlobalPosition);
            if (distSq <= nearestDistSq)
            {
                nearestDistSq = distSq;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private static bool Contains(IEnumerable<Enemy> collection, Enemy value)
    {
        foreach (Enemy enemy in collection)
        {
            if (enemy == value) return true;
        }

        return false;
    }
}
