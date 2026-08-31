using Godot;

public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }

    [Signal] public delegate void GamePhaseChangedEventHandler(GamePhase phase);
    [Signal] public delegate void PlacementModeChangedEventHandler(bool isActive);
    [Signal] public delegate void GameEndedEventHandler(bool victory);
    [Signal] public delegate void EnemyKilledEventHandler(int reward, int goldReward);
    [Signal] public delegate void PlayerHealthChangedEventHandler(float currentHealth, float maxHealth);

    public override void _Ready()
    {
        Instance = this;
    }
}
