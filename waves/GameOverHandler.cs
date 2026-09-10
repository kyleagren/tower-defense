using Godot;

public partial class GameOverHandler : Node
{
    [Export] public float ReloadDelay = 3f;
    [Export] public int GoldPerVictory = 100;
    [Export] public PackedScene HubScene { get; private set; }

    private bool _victoryPendingContinue;

    public override void _Ready()
    {
        EventBus.Instance.GameEnded += OnGameEnded;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GameEnded -= OnGameEnded;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_victoryPendingContinue && @event.IsActionPressed("start_wave"))
        {
            ReturnToHub();
        }
    }

    private void OnGameEnded(bool victory)
    {
        GD.Print(victory ? "Victory!" : "Defeat...");

        if (victory)
        {
            SaveManager.Instance.Data.Gold += GoldPerVictory + EconomyManager.Instance.GoldEarned;
            SaveManager.Instance.Save();
            GD.Print($"Gold: {SaveManager.Instance.Data.Gold}");

            // Wait for the player's own start_wave press before leaving - the level is already won
            // the moment the last wave clears, but they still want time to loot the drops first.
            _victoryPendingContinue = true;
            return;
        }

        GetTree().CreateTimer(ReloadDelay).Timeout += ReturnToHub;
    }

    private void ReturnToHub()
    {
        // Everything static that outlives the scene gets reset together here, so nothing from the
        // finished match can leak into the hub. Architect/Sentinel/SentinelSpawner's ByOwner
        // dictionaries are cleared here too - ChangeSceneToFile queues the old tree for deletion
        // rather than freeing it immediately, so a freed node's own _ExitTree unregistration can
        // land after the hub's fresh nodes have already registered the same PlayerId, leaving a
        // real window where a lookup like Architect.ByOwner[OwnerId] finds nothing.
        UiPanelStack.Reset();
        EnemyRegistry.Clear();
        IceWallSegmentRegistry.Clear();
        WorldItemRegistry.Clear();
        Architect.ByOwner.Clear();
        Sentinel.ByOwner.Clear();
        SentinelSpawner.ByOwner.Clear();

        GetTree().ChangeSceneToPacked(HubScene);
    }
}
