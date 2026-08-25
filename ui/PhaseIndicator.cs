using Godot;

public partial class PhaseIndicator : Control
{
    [Export] public Label PhaseLabel { get; private set; }

    private EconomyManager economy;
    private WaveController.GamePhase currentPhase = WaveController.GamePhase.Build;

    public override void _Ready()
    {
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        economy = GetTree().CurrentScene.GetNode<EconomyManager>("EconomyManager");
        UpdateText();
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
    }

    public override void _Process(double delta)
    {
        UpdateText();
    }

    private void OnGamePhaseChanged(WaveController.GamePhase phase)
    {
        currentPhase = phase;
    }

    private void UpdateText()
    {
        string phaseText = currentPhase == WaveController.GamePhase.Combat ? "Combat Phase" : "Build Phase";
        PhaseLabel.Text = $"{phaseText} — Budget: {economy.Budget} | Towers: {economy.CapacityUsed}/{economy.MaxCapacity}";
    }
}
