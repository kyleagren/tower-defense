using Godot;

[GlobalClass]
public partial class WaveSpawnEntry : Resource
{
    [Export] public string SpawnerId { get; private set; }
    [Export] public PackedScene EnemyScene { get; private set; }
    [Export] public int Count { get; private set; } = 1;
}
