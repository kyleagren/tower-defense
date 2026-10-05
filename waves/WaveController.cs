using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

// Runs a level's waves and owns its MatchState. Driven from _PhysicsProcess rather than async
// timers, so pausing the tree pauses the waves with it, and nothing keeps running against a scene
// that's been freed or reloaded. Only the host runs the match; everyone else sends their ready
// presses to it and follows the phase changes, ready state and ending it broadcasts.
public partial class WaveController : Node
{
    [Export] public string WaveFilePath = "res://assets/waves/test_level.json";
    [Export] public float SpawnInterval = 0.4f;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    // One lane's share of the current spawn step - what's left to spawn, trickled out on SpawnInterval.
    private class LaneQueue
    {
        public EnemySpawner Spawner;
        public Queue<string> Pending;
        public float Cooldown;
    }

    private MatchState _match;
    // This level's, kept rather than read through LevelContext.Current - during a level switch,
    // Current is already the incoming level for a frame.
    private LevelContext _level;
    private List<Wave> _waves = new();
    private int _nextWaveIndex;

    private Wave _activeWave;
    private int _stepIndex;
    private bool _isWaiting;
    private float _waitRemaining;
    private readonly List<LaneQueue> _laneQueues = new();

    // _EnterTree rather than _Ready, so the match exists before any node's _Ready reads it.
    public override void _EnterTree()
    {
        _match = new MatchState();
        _level = LevelContext.Current;
        _level.Match = _match;
        _match.ReadyCheckPassed += OnReadyCheckPassed;
        _match.ReadyStateChanged += OnReadyStateChanged;
        LoadWaves();
    }

    public override void _Ready()
    {
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        EventBus.Instance.GameEnded += OnGameEnded;

        // In case the host moved the match on before this level finished loading here.
        if (!NetworkSession.Instance.IsHost)
        {
            RpcId(1, MethodName.RequestMatchState);
        }
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
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
        _match.WaveCount = _waves.Count;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // D-pad right is also token 3 while R1 is held.
        if (!@event.IsActionPressed("start_wave") || InputChords.TokenModifierHeld)
        {
            return;
        }

        if (NetworkSession.Instance.IsHost)
        {
            _match.ToggleReady(PlayerProfiles.LocalId);
        }
        else
        {
            RpcId(1, MethodName.RequestToggleReady);
        }
    }

    // A client's own seat only - the seat comes from who sent it, not from anything it says.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestToggleReady()
    {
        PlayerId seat = NetworkSession.Instance.SenderSeat;

        if (NetworkSession.Instance.IsHost && seat != default)
        {
            _match.ToggleReady(seat);
        }
    }

    // Host only: every phase change, the ready state and the match ending go out to everyone else.
    private void OnGamePhaseChanged(GamePhase phase)
    {
        if (NetworkSession.Instance.IsHost)
        {
            Rpc(MethodName.ReceivePhase, (int)phase, _match.WavesStarted);
        }
    }

    private void OnReadyStateChanged()
    {
        if (NetworkSession.Instance.IsHost)
        {
            Rpc(MethodName.ReceiveReadyState, ReadySeats(), _match.ReadyCountdown ?? -1f);
        }
    }

    private void OnGameEnded(bool victory)
    {
        if (NetworkSession.Instance.IsHost)
        {
            Rpc(MethodName.ReceiveEnd, victory);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceivePhase(int phase, int wavesStarted)
    {
        _match.SetWavesStarted(wavesStarted);
        _match.SetPhase((GamePhase)phase);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveReadyState(int[] ready, float countdown)
    {
        _match.ApplyReadyState(ready.Select(seat => (PlayerId)seat), countdown < 0f ? null : countdown);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveEnd(bool victory)
    {
        _match.End(victory);
    }

    // Phase, then ready state, then the ending - the order the host went through them.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestMatchState()
    {
        if (!NetworkSession.Instance.IsHost)
        {
            return;
        }

        long sender = Multiplayer.GetRemoteSenderId();
        RpcId(sender, MethodName.ReceivePhase, (int)_match.Phase, _match.WavesStarted);
        RpcId(sender, MethodName.ReceiveReadyState, ReadySeats(), _match.ReadyCountdown ?? -1f);

        if (_match.IsOver)
        {
            RpcId(sender, MethodName.ReceiveEnd, _match.Victory);
        }
    }

    private int[] ReadySeats() => _match.ReadyPlayers.Select(seat => (int)seat).ToArray();

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

        _match.SetWavesStarted(_nextWaveIndex);
        _match.SetPhase(GamePhase.Combat);
        AdvanceStep();
    }

    public override void _PhysicsProcess(double delta)
    {
        // The host runs the match; everyone else follows what it broadcasts.
        if (!NetworkSession.Instance.IsHost)
        {
            _match.TickCountdownDisplay((float)delta);
            return;
        }

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
        if (_level.Enemies.Active.Count == 0)
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
            if (!_level.EnemySpawners.TryGetValue(laneGroups.Key, out EnemySpawner spawner))
            {
                GD.PushWarning($"WaveController: no spawner found with id \"{laneGroups.Key}\".");
                continue;
            }

            Queue<string> pending = new Queue<string>();

            foreach (WaveSpawnGroup group in laneGroups)
            {
                for (int i = 0; i < group.Count; i++)
                {
                    pending.Enqueue(group.Enemy);
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
        _level.Economy.AwardWaveClear();
        _match.SetPhase(isFinalWave ? GamePhase.PostGame : GamePhase.Build);

        if (isFinalWave)
        {
            _match.End(victory: true);
        }
    }
}
