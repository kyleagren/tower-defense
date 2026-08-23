using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class WaveController : Node
{
    public enum WavePhase
    {
        Build,
        Wave
    }

    [Export] public Godot.Collections.Array<WaveDefinition> Waves { get; private set; } = new();
    [Export] public float SpawnInterval = 0.3f;

    private const string SpawnerGroup = "enemy_spawners";

    private Dictionary<string, EnemySpawner> spawnersById;
    private int nextWaveIndex;
    private WavePhase currentPhase = WavePhase.Build;

    public override void _Ready()
    {
        spawnersById = new Dictionary<string, EnemySpawner>();

        foreach (Node node in GetTree().GetNodesInGroup(SpawnerGroup))
        {
            if (node is EnemySpawner spawner)
            {
                spawnersById[spawner.Id] = spawner;
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Space)
        {
            StartNextWave();
        }
    }

    public void StartNextWave()
    {
        if (currentPhase != WavePhase.Build || nextWaveIndex >= Waves.Count)
        {
            return;
        }

        WaveDefinition wave = Waves[nextWaveIndex];
        nextWaveIndex++;

        _ = RunWave(wave);
    }

    private async Task RunWave(WaveDefinition wave)
    {
        currentPhase = WavePhase.Wave;
        EventBus.Instance.EmitSignal(EventBus.SignalName.WavePhaseChanged, (int)WavePhase.Wave);

        Dictionary<string, List<WaveSpawnEntry>> entriesBySpawner = new Dictionary<string, List<WaveSpawnEntry>>();

        foreach (WaveSpawnEntry entry in wave.Entries)
        {
            if (!entriesBySpawner.TryGetValue(entry.SpawnerId, out List<WaveSpawnEntry> entries))
            {
                entries = new List<WaveSpawnEntry>();
                entriesBySpawner[entry.SpawnerId] = entries;
            }

            entries.Add(entry);
        }

        List<Task> spawnerTasks = new List<Task>();

        foreach (KeyValuePair<string, List<WaveSpawnEntry>> pair in entriesBySpawner)
        {
            spawnerTasks.Add(RunSpawnerQueue(pair.Key, pair.Value, wave.RandomizeSpawnOrder));
        }

        await Task.WhenAll(spawnerTasks);

        currentPhase = WavePhase.Build;
        EventBus.Instance.EmitSignal(EventBus.SignalName.WavePhaseChanged, (int)WavePhase.Build);
    }

    private async Task RunSpawnerQueue(string spawnerId, List<WaveSpawnEntry> entries, bool randomizeOrder)
    {
        if (!spawnersById.TryGetValue(spawnerId, out EnemySpawner spawner))
        {
            GD.PushWarning($"WaveController: no spawner found with id \"{spawnerId}\".");
            return;
        }

        List<PackedScene> spawnQueue = new List<PackedScene>();

        foreach (WaveSpawnEntry entry in entries)
        {
            for (int i = 0; i < entry.Count; i++)
            {
                spawnQueue.Add(entry.EnemyScene);
            }
        }

        if (randomizeOrder)
        {
            for (int i = spawnQueue.Count - 1; i > 0; i--)
            {
                int j = GD.RandRange(0, i);
                (spawnQueue[i], spawnQueue[j]) = (spawnQueue[j], spawnQueue[i]);
            }
        }

        foreach (PackedScene enemyScene in spawnQueue)
        {
            spawner.Spawn(enemyScene);
            await ToSignal(GetTree().CreateTimer(SpawnInterval), "timeout");
        }
    }
}
