using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

// Runs a level's waves and owns its MatchState. Driven from _PhysicsProcess rather than async
// timers, so pausing the tree pauses the waves with it, and nothing keeps running against a scene
// that's been freed or reloaded.
public partial class WaveController : Node
{
    [Export] public string WaveFilePath = "res://assets/waves/test_level.json";
    [Export] public float SpawnInterval = 0.3f;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static WaveController Instance { get; private set; }

    // One lane's share of the current spawn step - what's left to spawn, trickled out on SpawnInterval.
    private class LaneQueue
    {
        public EnemySpawner Spawner;
        public Queue<PackedScene> Pending;
        public float Cooldown;
    }

    private MatchState _match;
    private List<Wave> _waves = new();
    private Dictionary<string, EnemySpawner> _spawnersById;
    private int _nextWaveIndex;

    private Wave _activeWave;
    private int _stepIndex;
    private bool _isWaiting;
    private float _waitRemaining;
    private readonly List<LaneQueue> _laneQueues = new();

    // _EnterTree rather than _Ready, so the match exists before any node's _Ready reads it.
    public override void _EnterTree()
    {
        Instance = this;
        _match = new MatchState(LevelContext.Current);
        LevelContext.Current.Match = _match;
        _match.ReadyCheckPassed += OnReadyCheckPassed;
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

    public override void _UnhandledInput(InputEvent @event)
    {
        // D-pad right is also token 3 while R1 is held.
        if (@event.IsActionPressed("start_wave") && !InputChords.TokenModifierHeld)
        {
            _match.ToggleReady(PlayerProfiles.LocalId);
        }
    }

    // PostGame's "move on" belongs to GameOverHandler, which listens for the same signal.
    private void OnReadyCheckPassed(GamePhase phase)
    {
        if (phase == GamePhase.Planning)
        {
            _match.SetPhase(GamePhase.Build);
        }
        else if (phase == GamePhase.Build)
        {
            StartNextWave();
        }
    }

    public void StartNextWave()
    {
        if (_match.Phase != GamePhase.Build || _nextWaveIndex >= _waves.Count)
        {
            return;
        }

        BeginWave(_waves[_nextWaveIndex++]);
    }

    private void BeginWave(Wave wave)
    {
        if (_match.IsOver)
        {
            return;
        }

        _activeWave = wave;
        _stepIndex = -1;
        _isWaiting = false;
        _laneQueues.Clear();

        _match.SetPhase(GamePhase.Combat);
        AdvanceStep();
    }

    public override void _PhysicsProcess(double delta)
    {
        _match.Tick((float)delta);

        if (_activeWave == null || _match.IsOver)
        {
            return;
        }

        if (_laneQueues.Count > 0)
        {
            TickLaneQueues((float)delta);

            if (_laneQueues.Count == 0)
            {
                AdvanceStep();
            }

            return;
        }

        if (_isWaiting)
        {
            _waitRemaining -= (float)delta;

            if (_waitRemaining <= 0f)
            {
                _isWaiting = false;
                AdvanceStep();
            }

            return;
        }

        // Every step has run - the wave ends once the last enemy it spawned is gone.
        if (LevelContext.Current.Enemies.Active.Count == 0)
        {
            EndWave();
        }
    }

    // Moves to the next step: a spawn burst fills the lane queues, a wait starts the timer. A burst
    // with nothing to spawn (every lane id unknown) falls straight through to the step after it.
    private void AdvanceStep()
    {
        while (++_stepIndex < _activeWave.Steps.Count)
        {
            WaveStep step = _activeWave.Steps[_stepIndex];

            if (step.Wait.HasValue)
            {
                _waitRemaining = step.Wait.Value;
                _isWaiting = true;
                return;
            }

            QueueSpawnStep(step.Spawn);

            if (_laneQueues.Count > 0)
            {
                return;
            }
        }
    }

    private void QueueSpawnStep(List<WaveSpawnGroup> groups)
    {
        foreach (IGrouping<string, WaveSpawnGroup> laneGroups in groups.GroupBy(group => group.Lane))
        {
            if (!_spawnersById.TryGetValue(laneGroups.Key, out EnemySpawner spawner))
            {
                GD.PushWarning($"WaveController: no spawner found with id \"{laneGroups.Key}\".");
                continue;
            }

            Queue<PackedScene> pending = new Queue<PackedScene>();

            foreach (WaveSpawnGroup group in laneGroups)
            {
                PackedScene scene = EnemyCatalog.Get(group.Enemy);

                for (int i = 0; i < group.Count; i++)
                {
                    pending.Enqueue(scene);
                }
            }

            _laneQueues.Add(new LaneQueue { Spawner = spawner, Pending = pending });
        }
    }

    // Each lane trickles its burst out on SpawnInterval so it doesn't spawn as one stacked pile,
    // lanes in parallel. A lane counts as done one interval after its last spawn, so a "wait 10"
    // after a burst means 10 seconds after that burst actually finished spawning.
    private void TickLaneQueues(float delta)
    {
        for (int i = _laneQueues.Count - 1; i >= 0; i--)
        {
            LaneQueue lane = _laneQueues[i];
            lane.Cooldown -= delta;

            if (lane.Cooldown > 0f)
            {
                continue;
            }

            if (lane.Pending.Count == 0)
            {
                _laneQueues.RemoveAt(i);
                continue;
            }

            lane.Spawner.Spawn(lane.Pending.Dequeue());
            lane.Cooldown = SpawnInterval;
        }
    }

    private void EndWave()
    {
        _activeWave = null;

        bool isFinalWave = _nextWaveIndex >= _waves.Count;
        _match.SetPhase(isFinalWave ? GamePhase.PostGame : GamePhase.Build);

        if (isFinalWave)
        {
            _match.End(victory: true);
        }
    }
}
