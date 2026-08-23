using Godot;

public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }

    [Signal] public delegate void WavePhaseChangedEventHandler(WaveController.WavePhase phase);

    public override void _Ready()
    {
        Instance = this;
    }
}
