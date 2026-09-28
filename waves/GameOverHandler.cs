using Godot;

public partial class GameOverHandler : Node
{
    [Export] public float ReloadDelay = 3f;
    [Export] public int GoldPerVictory = 100;

    public override void _Ready()
    {
        EventBus.Instance.GameEnded += OnGameEnded;
        EventBus.Instance.ReturnToHubRequested += ReturnToHub;
        LevelContext.Current.Match.ReadyCheckPassed += OnReadyCheckPassed;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GameEnded -= OnGameEnded;
        EventBus.Instance.ReturnToHubRequested -= ReturnToHub;
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

        if (!victory)
        {
            GetTree().CreateTimer(ReloadDelay).Timeout += ReturnToHub;
            return;
        }

        // The host counts the gold earned, so it works out the award and sends it rather than each
        // machine reading its mirrored copy, which could still be a kill behind.
        if (NetworkSession.Instance.IsHost)
        {
            int gold = GoldPerVictory + EconomyManager.Instance.GoldEarned;
            AwardGold(gold);
            Rpc(MethodName.ReceiveVictoryGold, gold);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveVictoryGold(int gold)
    {
        AwardGold(gold);
    }

    private void AwardGold(int gold)
    {
        PlayerProfiles.Local.AddGold(gold);
        GD.Print($"Gold: {PlayerProfiles.Local.Data.Gold}");
    }

    // Host only, through LevelLoader - everyone else follows.
    private void ReturnToHub()
    {
        LevelLoader.Instance.LoadHub();
    }
}
