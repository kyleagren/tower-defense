using Godot;

public partial class GameOverHandler : Node
{
    [Export] public float ReloadDelay = 3f;
    [Export] public int GoldPerVictory = 100;
    [Export] public PackedScene HubScene { get; private set; }

    public override void _Ready()
    {
        EventBus.Instance.GameEnded += OnGameEnded;
        LevelContext.Current.Match.ReadyCheckPassed += OnReadyCheckPassed;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GameEnded -= OnGameEnded;
    }

    // Leaving PostGame goes through the same ready check as every other "move on" - the level is
    // already won the moment the last wave clears, but players still want time to loot first.
    private void OnReadyCheckPassed(GamePhase phase)
    {
        if (phase == GamePhase.PostGame)
        {
            ReturnToHub();
        }
    }

    private void OnGameEnded(bool victory)
    {
        GD.Print(victory ? "Victory!" : "Defeat...");

        if (victory)
        {
            PlayerProfiles.Local.AddGold(GoldPerVictory + EconomyManager.Instance.GoldEarned);
            GD.Print($"Gold: {PlayerProfiles.Local.Data.Gold}");
            return;
        }

        GetTree().CreateTimer(ReloadDelay).Timeout += ReturnToHub;
    }

    private void ReturnToHub()
    {
        GetTree().ChangeSceneToPacked(HubScene);
    }
}
