using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class WaveController : Node
{
    [Export] public string WaveFilePath = "res://assets/waves/level_01.json";
    [Export] public float SpawnInterval = 0.3f;
    [Export] public float EnemyCheckInterval = 0.5f;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static WaveController Instance { get; private set; }

    private List<Wave> _waves = new();
    private Dictionary<string, EnemySpawner> _spawnersById;
    private int _nextWaveIndex;
    private GamePhase _currentPhase = GamePhase.Planning;
    private bool _matchOver;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        LoadWaves();

        _spawnersById = new Dictionary<string, EnemySpawner>();

        foreach (Node node in GetTree().GetNodesInGroup(Groups.EnemySpawners))
        {
            if (node is EnemySpawner spawner)
            {
                _spawnersById[spawner.Id] = spawner;
            }
        }

        EventBus.Instance.GameEnded += OnGameEnded;

        EmitInitialPhase();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GameEnded -= OnGameEnded;
    }

    private void LoadWaves()
    {
        using FileAccess file = FileAccess.Open(WaveFilePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"WaveController: failed to open wave file \"{WaveFilePath}\".");
            return;
        }

        string json = file.GetAsText();
        WaveFile waveFile = JsonSerializer.Deserialize<WaveFile>(json, _jsonOptions);
        _waves = waveFile.Waves;
    }

    // A single CallDeferred only guarantees running after this node's own _Ready - it doesn't
    // guarantee running after OTHER nodes' deferred work, like SentinelSpawner's own deferred spawn
    // of the Sentinel that needs to hear this. Waiting a full process frame does.
    private async void EmitInitialPhase()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        EventBus.Instance.EmitSignal(EventBus.SignalName.GamePhaseChanged, (int)_currentPhase);
    }

    // RunWave is a detached Task, so losing the match doesn't unwind it - without this it keeps
    // spawning into a lost level for the whole of GameOverHandler's reload delay.
    private void OnGameEnded(bool victory)
    {
        _matchOver = true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("start_wave"))
        {
            return;
        }

        // The first press each level just ends Planning and hands control to the Architect - it
        // doesn't spawn anything on its own. The next press (now in Build) is what starts a wave.
        if (_currentPhase == GamePhase.Planning)
        {
            _currentPhase = GamePhase.Build;
            EventBus.Instance.EmitSignal(EventBus.SignalName.GamePhaseChanged, (int)GamePhase.Build);
            return;
        }

        StartNextWave();
    }

    // Debug-only: swaps in a different wave file and immediately starts its first wave, skipping
    // the normal Build-phase "press start_wave" step - for quickly testing a specific enemy/spawn
    // setup without waiting through the level's real wave sequence.
    public void DebugLoadAndStartWaveFile(string path)
    {
        WaveFilePath = path;
        LoadWaves();
        _nextWaveIndex = 0;
        _currentPhase = GamePhase.Build;
        StartNextWave();
    }

    public void StartNextWave()
    {
        if (_currentPhase != GamePhase.Build || _nextWaveIndex >= _waves.Count)
        {
            return;
        }

        Wave wave = _waves[_nextWaveIndex];
        _nextWaveIndex++;

        _ = RunWave(wave);
    }

    private async Task RunWave(Wave wave)
    {
        _currentPhase = GamePhase.Combat;
        EventBus.Instance.EmitSignal(EventBus.SignalName.GamePhaseChanged, (int)GamePhase.Combat);

        foreach (WaveStep step in wave.Steps)
        {
            if (_matchOver)
            {
                return;
            }

            if (step.Wait.HasValue)
            {
                await ToSignal(GetTree().CreateTimer(step.Wait.Value), "timeout");
                continue;
            }

            await RunSpawnStep(step.Spawn);
        }

        while (!_matchOver && EnemyRegistry.Active.Count > 0)
        {
            await ToSignal(GetTree().CreateTimer(EnemyCheckInterval), "timeout");
        }

        if (_matchOver)
        {
            return;
        }

        bool isFinalWave = _nextWaveIndex >= _waves.Count;
        _currentPhase = isFinalWave ? GamePhase.PostGame : GamePhase.Build;
        EventBus.Instance.EmitSignal(EventBus.SignalName.GamePhaseChanged, (int)_currentPhase);

        if (isFinalWave)
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.GameEnded, true);
        }
    }

    // Each lane's groups within a step still trickle out on SpawnInterval (so a burst doesn't spawn
    // as one stacked pile) and different lanes still run in parallel - but the whole step is awaited
    // before the next one (e.g. a wait) begins, so a "wait 10" after a burst means 10 seconds after
    // everyone in that burst has actually finished spawning.
    private async Task RunSpawnStep(List<WaveSpawnGroup> groups)
    {
        List<Task> spawnerTasks = new List<Task>();

        foreach (IGrouping<string, WaveSpawnGroup> laneGroups in groups.GroupBy(group => group.Lane))
        {
            spawnerTasks.Add(RunSpawnerQueue(laneGroups.Key, laneGroups.ToList()));
        }

        await Task.WhenAll(spawnerTasks);
    }

    private async Task RunSpawnerQueue(string spawnerId, List<WaveSpawnGroup> groups)
    {
        if (!_spawnersById.TryGetValue(spawnerId, out EnemySpawner spawner))
        {
            GD.PushWarning($"WaveController: no spawner found with id \"{spawnerId}\".");
            return;
        }

        List<PackedScene> spawnQueue = new List<PackedScene>();

        foreach (WaveSpawnGroup group in groups)
        {
            PackedScene scene = EnemyCatalog.Get(group.Enemy);
            for (int i = 0; i < group.Count; i++)
            {
                spawnQueue.Add(scene);
            }
        }

        foreach (PackedScene enemyScene in spawnQueue)
        {
            if (_matchOver)
            {
                return;
            }

            spawner.Spawn(enemyScene);
            await ToSignal(GetTree().CreateTimer(SpawnInterval), "timeout");
        }
    }
}
