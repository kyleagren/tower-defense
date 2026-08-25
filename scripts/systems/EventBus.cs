using Godot;

public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }

    [Signal] public delegate void GamePhaseChangedEventHandler(WaveController.GamePhase phase);
    [Signal] public delegate void PlacementModeChangedEventHandler(bool isActive);

    public override void _Ready()
    {
        Instance = this;
    }
}
