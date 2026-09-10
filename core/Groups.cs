// Scene-tree group names, in one place so renaming one is a single edit rather than a grep across
// unrelated files. Groups are used for editor / remote-debugger inspection and for finding a
// handful of level nodes at startup - the per-frame lookups go through ActiveRegistry instead.
public static class Groups
{
    public const string EnemySpawners = "enemy_spawners";
    public const string Enemies = "enemies";
}
