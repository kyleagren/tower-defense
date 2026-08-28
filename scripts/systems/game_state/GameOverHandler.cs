using Godot;

public partial class GameOverHandler : Node
{
    [Export] public float ReloadDelay = 3f;

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
        GetTree().CreateTimer(ReloadDelay).Timeout += () => GetTree().ReloadCurrentScene();
    }
}
