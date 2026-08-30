using Godot;

public partial class PhaseIndicator : Control
{
    [Export] public Label PhaseLabel { get; private set; }
    [Export] public Label GoldLabel { get; private set; }

    private WaveController.GamePhase _currentPhase = WaveController.GamePhase.Build;

    public override void _Ready()
    {
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        EconomyManager.Instance.ManaChanged += UpdateText;
        EconomyManager.Instance.GoldEarnedChanged += UpdateGoldText;
        UpdateText();
        UpdateGoldText();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
        EconomyManager.Instance.ManaChanged -= UpdateText;
        EconomyManager.Instance.GoldEarnedChanged -= UpdateGoldText;
    }

    private void OnGamePhaseChanged(WaveController.GamePhase phase)
    {
        _currentPhase = phase;
        UpdateText();
    }

    private void UpdateText()
    {
        string phaseText = _currentPhase == WaveController.GamePhase.Combat ? "Combat Phase" : "Build Phase";
        PhaseLabel.Text = $"{phaseText} — Mana: {EconomyManager.Instance.Mana} | Build Capacity: {EconomyManager.Instance.CapacityUsed}/{EconomyManager.Instance.MaxCapacity}";
    }

    private void UpdateGoldText()
    {
        int total = SaveManager.Instance.Data.Gold + EconomyManager.Instance.GoldEarned;
        GoldLabel.Text = $"Gold: {total}";
    }
}
