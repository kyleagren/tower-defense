using Godot;

public partial class GameOverHandler : Node
{
    [Export] public float ReloadDelay = 3f;
    [Export] public int GoldPerVictory = 100;

    public override void _Ready()
    {
        EventBus.Instance.GameEnded += OnGameEnded;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GameEnded -= OnGameEnded;
    }

    private void OnGameEnded(bool victory)
    {
        GD.Print(victory ? "Victory!" : "Defeat...");

        if (victory)
        {
            SaveManager.Instance.Data.Gold += GoldPerVictory + EconomyManager.Instance.GoldEarned;
            SaveManager.Instance.Save();
            GD.Print($"Gold: {SaveManager.Instance.Data.Gold}");
        }

        GetTree().CreateTimer(ReloadDelay).Timeout += () =>
        {
            UiPanelStack.Reset();
            GetTree().ReloadCurrentScene();
        };
    }
}
