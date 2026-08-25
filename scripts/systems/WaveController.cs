using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class WaveController : Node
{
    public enum GamePhase
    {
        Build,
        Combat
    }

    [Export] public Godot.Collections.Array<WaveDefinition> Waves { get; private set; } = new();
    [Export] public float SpawnInterval = 0.3f;
    [Export] public float EnemyCheckInterval = 0.5f;

    private const string SpawnerGroup = "enemy_spawners";
    private const string EnemyGroup = "enemies";

    private Dictionary<string, EnemySpawner> spawnersById;
    private int nextWaveIndex;
    private GamePhase currentPhase = GamePhase.Build;

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
        if (@event.IsActionPressed("start_wave"))
        {
            StartNextWave();
        }
    }

    public void StartNextWave()
    {
        if (currentPhase != GamePhase.Build || nextWaveIndex >= Waves.Count)
        {
            return;
        }

        WaveDefinition wave = Waves[nextWaveIndex];
        nextWaveIndex++;

        _ = RunWave(wave);
    }

    private async Task RunWave(WaveDefinition wave)
    {
        currentPhase = GamePhase.Combat;
        EventBus.Instance.EmitSignal(EventBus.SignalName.GamePhaseChanged, (int)GamePhase.Combat);

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

        while (GetTree().GetNodesInGroup(EnemyGroup).Count > 0)
        {
            await ToSignal(GetTree().CreateTimer(EnemyCheckInterval), "timeout");
        }

        currentPhase = GamePhase.Build;
        EventBus.Instance.EmitSignal(EventBus.SignalName.GamePhaseChanged, (int)GamePhase.Build);
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
