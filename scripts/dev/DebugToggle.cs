using Godot;

public partial class DebugToggle : Node
{
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.PhysicalKeycode == Key.F1)
        {
            DebugSettings.ShowEnemyHealthBars = !DebugSettings.ShowEnemyHealthBars;

            foreach (Node node in GetTree().GetNodesInGroup("enemies"))
            {
                if (node is Enemy enemy && !enemy.IsDead)
                {
                    enemy.HealthBar.Visible = DebugSettings.ShowEnemyHealthBars;
                }
            }
        }
    }
}
