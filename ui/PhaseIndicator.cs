using Godot;

public partial class PhaseIndicator : Control
{
    [Export] public Label PhaseLabel { get; private set; }
    [Export] public Label GoldLabel { get; private set; }

    public override void _Ready()
    {
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        EconomyManager.Instance.EconomyChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
        EconomyManager.Instance.EconomyChanged -= Refresh;
    }

    private void OnGamePhaseChanged(GamePhase phase) => Refresh();

    private void Refresh()
    {
        GamePhase phase = LevelContext.Current.Match.Phase;
        string phaseText = phase switch
        {
            GamePhase.Planning => "Planning Phase",
            GamePhase.Build => "Build Phase",
            GamePhase.Combat => "Combat Phase",
            GamePhase.PostGame => "Post-Game",
            _ => phase.ToString()
        };

        PhaseLabel.Text = $"{phaseText} — Mana: {EconomyManager.Instance.Mana} | Build Capacity: {EconomyManager.Instance.CapacityUsed}/{EconomyManager.Instance.MaxCapacity}";

        int totalGold = SaveManager.Instance.Data.Gold + EconomyManager.Instance.GoldEarned;
        GoldLabel.Text = $"Gold: {totalGold}";
    }
}
