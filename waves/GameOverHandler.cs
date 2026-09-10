using Godot;

public partial class GameOverHandler : Node
{
    [Export] public float ReloadDelay = 3f;
    [Export] public int GoldPerVictory = 100;

    private const string HubScenePath = "res://scenes/hub.tscn";

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
        // finished match can leak into the hub.
        UiPanelStack.Reset();
        EnemyRegistry.Clear();
        IceWallSegmentRegistry.Clear();
        WorldItemRegistry.Clear();

        GetTree().ChangeSceneToFile(HubScenePath);
    }
}
