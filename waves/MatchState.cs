using System;
using System.Collections.Generic;

// Where a level's match stands: its phase, whether it's over, and who's ready to move on. The only
// place any of those change, so each "phase changed" and "match ended" notification fires exactly
// once, and anything arriving late - a Sentinel spawned a frame after load - reads the current state
// instead of waiting on an event it already missed.
public class MatchState
{
    private readonly HashSet<PlayerId> _ready = new();

    public GamePhase Phase { get; private set; } = GamePhase.Planning;
    public bool IsOver { get; private set; }

    // Meaningful once IsOver.
    public bool Victory { get; private set; }

    // How many waves have begun, and how many the level's wave file holds.
    public int WavesStarted { get; private set; }
    public int WaveCount { get; set; }

    public IReadOnlyCollection<PlayerId> ReadyPlayers => _ready;

    // Seconds until the ready check passes on its own - null until the host readies up. It exists so
    // a player who crashed or left can't hold everyone else in place forever.
    public float? ReadyCountdown { get; private set; }

    // Fires with the phase the check was for; whoever owns that phase's "move on" acts on it.
    public event Action<GamePhase> ReadyCheckPassed;

    // Whenever who's ready changes, or the countdown starts or stops - not every tick of it.
    public event Action ReadyStateChanged;

    // Ready checks gate leaving Planning, starting a wave from Build and leaving PostGame - never Combat.
    public bool CanReadyUp => Phase != GamePhase.Combat;

    public void SetPhase(GamePhase phase)
    {
        if (IsOver || phase == Phase)
        {
            return;
        }

        Phase = phase;
        ClearReadyCheck();
        EventBus.Instance.EmitSignal(EventBus.SignalName.GamePhaseChanged, (int)phase);
    }

    public void SetWavesStarted(int count)
    {
        WavesStarted = count;
    }

    // First call wins: the objective falling and the Sentinel dying in the same wave is one loss.
    public void End(bool victory)
    {
        if (IsOver)
        {
            return;
        }

        IsOver = true;
        Victory = victory;
        EventBus.Instance.EmitSignal(EventBus.SignalName.GameEnded, victory);
    }

    public void ToggleReady(PlayerId player)
    {
        if (!CanReadyUp)
        {
            return;
        }

        if (_ready.Remove(player))
        {
            if (player == PlayerProfiles.HostId)
            {
                ReadyCountdown = null;
            }

            ReadyStateChanged?.Invoke();
            return;
        }

        _ready.Add(player);

        if (player == PlayerProfiles.HostId)
        {
            ReadyCountdown = GameRules.ReadyCountdownSeconds;
        }

        ReadyStateChanged?.Invoke();
        TryPassReadyCheck();
    }

    public void Tick(float delta)
    {
        if (ReadyCountdown.HasValue)
        {
            ReadyCountdown -= delta;

            if (ReadyCountdown <= 0f)
            {
                PassReadyCheck();
                return;
            }
        }

        // Re-checked every tick, not only on a toggle, so a player leaving mid-check doesn't leave
        // everyone else waiting on someone who's gone.
        TryPassReadyCheck();
    }

    // A client's copy of the host's ready check. Clients never tick the real check - the countdown
    // just runs down here for the phase bar.
    public void ApplyReadyState(IEnumerable<PlayerId> ready, float? countdown)
    {
        _ready.Clear();
        _ready.UnionWith(ready);
        ReadyCountdown = countdown;
        ReadyStateChanged?.Invoke();
    }

    public void TickCountdownDisplay(float delta)
    {
        if (ReadyCountdown.HasValue)
        {
            ReadyCountdown = Math.Max(0f, ReadyCountdown.Value - delta);
        }
    }

    private void TryPassReadyCheck()
    {
        if (_ready.Count == 0)
        {
            return;
        }

        foreach (PlayerId player in NetworkSession.Instance.FilledSeats)
        {
            if (!_ready.Contains(player))
            {
                return;
            }
        }

        PassReadyCheck();
    }

    private void PassReadyCheck()
    {
        GamePhase phase = Phase;
        ClearReadyCheck();
        ReadyCheckPassed?.Invoke(phase);
    }

    private void ClearReadyCheck()
    {
        _ready.Clear();
        ReadyCountdown = null;
        ReadyStateChanged?.Invoke();
    }
}
