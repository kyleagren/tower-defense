using System.Collections.Generic;

// Live enemies, maintained by Enemy itself. Replaces GetNodesInGroup("enemies") on the targeting
// hot path: no per-call Godot Array allocation, no marshalling of Node handles across the engine
// boundary, and no per-element type check. Enemies leave the moment they die, so ragdolls waiting
// on their cleanup timer are never considered as targets.
public static class EnemyRegistry
{
    private static readonly List<Enemy> _active = new List<Enemy>();

    public static IReadOnlyList<Enemy> Active => _active;

    public static void Register(Enemy enemy)
    {
        _active.Add(enemy);
    }

    public static void Unregister(Enemy enemy)
    {
        _active.Remove(enemy);
    }
}
