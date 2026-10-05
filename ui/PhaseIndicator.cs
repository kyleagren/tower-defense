using Godot;
using System.Linq;
using System.Text;

public partial class PhaseIndicator : Control
{
    [Export] public Label PhaseLabel { get; private set; }
    [Export] public Label GoldLabel { get; private set; }
    [Export] public RichTextLabel ReadyLabel { get; private set; }

    private static readonly Color ReadyColor = new Color(0.4f, 0.85f, 0.4f);
    private static readonly Color NotReadyColor = new Color(0.9f, 0.35f, 0.35f);

    // This level's match and economy, kept from _Ready rather than read through LevelContext.Current
    // each time - for a frame during a level switch, Current already points at the incoming level.
    // Null in the hub.
    private MatchState _match;
    private EconomyManager _economy;

    public override void _Ready()
    {
        _match = LevelContext.Current.Match;

        if (_match == null)
        {
            ReadyLabel.Visible = false;
            EventBus.Instance.InventoryChanged += RefreshHub;
            RefreshHub();
            return;
        }

        _economy = LevelContext.Current.Economy;
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        _economy.EconomyChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_match == null)
        {
            EventBus.Instance.InventoryChanged -= RefreshHub;
            return;
        }

        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
        _economy.EconomyChanged -= Refresh;
    }

    // Polled rather than event-driven, since the countdown runs down continuously.
    public override void _Process(double delta)
    {
        if (_match != null)
        {
            RefreshReady();
        }
    }

    // Each player's seat, green once they're ready and red until then, then the countdown once the
    // host has readied. Hidden in Combat, where there's nothing to ready up for.
    private void RefreshReady()
    {
        ReadyLabel.Visible = _match.CanReadyUp;

        if (!ReadyLabel.Visible)
        {
            return;
        }

        StringBuilder text = new StringBuilder();

        foreach (PlayerId seat in NetworkSession.Instance.FilledSeats.OrderBy(seat => seat))
        {
            bool ready = _match.ReadyPlayers.Contains(seat);
            Color color = ready ? ReadyColor : NotReadyColor;
            text.Append($"[color=#{color.ToHtml(false)}]{(ready ? "✓" : "✗")} {seat}[/color]  ");
        }

        if (_match.ReadyCountdown.HasValue)
        {
            text.Append($"{Mathf.CeilToInt(_match.ReadyCountdown.Value)}s");
        }

        string result = text.ToString();

        if (ReadyLabel.Text != result)
        {
            ReadyLabel.Text = result;
        }
    }

    private void OnGamePhaseChanged(GamePhase phase) => Refresh();

    private void Refresh()
    {
        GamePhase phase = _match.Phase;
        string phaseText = phase switch
        {
            GamePhase.Planning => "Planning Phase",
            GamePhase.Build => "Build Phase",
            GamePhase.Combat => "Combat Phase",
            GamePhase.PostGame => "Post-Game",
            _ => phase.ToString()
        };

        PhaseLabel.Text = $"Wave {DisplayedWave}/{_match.WaveCount} — {phaseText} — Mana: {_economy.Mana} | Build Capacity: {_economy.CapacityUsed}/{_economy.MaxCapacity}";

        int totalGold = PlayerProfiles.Local.Data.Gold + _economy.GoldEarned;
        GoldLabel.Text = $"Gold: {totalGold}";
    }

    // The wave being fought, or in Planning and Build the one coming next.
    private int DisplayedWave => _match.Phase is GamePhase.Combat or GamePhase.PostGame
        ? _match.WavesStarted
        : Mathf.Min(_match.WavesStarted + 1, _match.WaveCount);

    // The hub has no match, so no mana or capacity to run out of; gold is only what's in the profile.
    private void RefreshHub()
    {
        PhaseLabel.Text = "The Hub — Mana: ∞ | Build Capacity: ∞";
        GoldLabel.Text = $"Gold: {PlayerProfiles.Local.Data.Gold}";
    }
}
