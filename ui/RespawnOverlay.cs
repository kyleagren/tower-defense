using Godot;

// Center-screen countdown and skip prompt while the local Sentinel is dead. Polled rather than
// event-driven, like AbilityBar: the countdown is a continuous quantity with no "changed" signal.
public partial class RespawnOverlay : Control
{
    [Export] public Label CountdownLabel { get; private set; }
    [Export] public Label SkipLabel { get; private set; }

    private static readonly Color _unaffordableColor = new Color(1f, 0.3f, 0.3f);

    public override void _Ready()
    {
        Visible = false;
    }

    public override void _Process(double delta)
    {
        Sentinel sentinel = Sentinel.Local;
        Visible = sentinel != null && sentinel.IsDead;

        if (!Visible)
        {
            return;
        }

        int skipCost = sentinel.RespawnSkipCost;

        CountdownLabel.Text = $"Respawning in {Mathf.CeilToInt(sentinel.RespawnRemaining)}";
        SkipLabel.Text = $"Press {InputPromptHelper.GetPrompt("interact")} to respawn now ({skipCost} mana)";
        SkipLabel.Modulate = LevelContext.Current.Economy.CanAffordMana(skipCost) ? Colors.White : _unaffordableColor;
    }
}
