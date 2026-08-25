using Godot;

public partial class PhaseIndicator : Control
{
    [Export] public Label PhaseLabel { get; private set; }

    public override void _Ready()
    {
        EventBus.Instance.WavePhaseChanged += OnWavePhaseChanged;
        UpdateText(WaveController.WavePhase.Build);
    }

    private void OnWavePhaseChanged(WaveController.WavePhase phase)
    {
        UpdateText(phase);
    }

    private void UpdateText(WaveController.WavePhase phase)
    {
        PhaseLabel.Text = phase == WaveController.WavePhase.Wave ? "Combat Phase" : "Build Phase";
    }
}
