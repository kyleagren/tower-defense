using Godot;

public partial class PhaseIndicator : Control
{
    [Export] public Label PhaseLabel { get; private set; }

    private WaveController.GamePhase _currentPhase = WaveController.GamePhase.Build;

    public override void _Ready()
    {
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        EconomyManager.Instance.BudgetChanged += UpdateText;
        UpdateText();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
        EconomyManager.Instance.BudgetChanged -= UpdateText;
    }

    private void OnGamePhaseChanged(WaveController.GamePhase phase)
    {
        _currentPhase = phase;
        UpdateText();
    }

    private void UpdateText()
    {
        string phaseText = _currentPhase == WaveController.GamePhase.Combat ? "Combat Phase" : "Build Phase";
        PhaseLabel.Text = $"{phaseText} — Budget: {EconomyManager.Instance.Budget} | Towers: {EconomyManager.Instance.CapacityUsed}/{EconomyManager.Instance.MaxCapacity}";
    }
}
