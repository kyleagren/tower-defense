using Godot;

public partial class PhaseIndicator : Control
{
    [Export] public Label PhaseLabel { get; private set; }

    public override void _Ready()
    {
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        UpdateText(WaveController.GamePhase.Build);
    }

    private void OnGamePhaseChanged(WaveController.GamePhase phase)
    {
        UpdateText(phase);
    }

    private void UpdateText(WaveController.GamePhase phase)
    {
        PhaseLabel.Text = phase == WaveController.GamePhase.Combat ? "Combat Phase" : "Build Phase";
    }
}
