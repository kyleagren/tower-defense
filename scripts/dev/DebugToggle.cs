using Godot;

public partial class DebugToggle : Node
{
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        if (keyEvent.PhysicalKeycode == Key.F1)
        {
            DebugSettings.ShowEnemyHealthBars = !DebugSettings.ShowEnemyHealthBars;

            foreach (Enemy enemy in EnemyRegistry.Active)
            {
                enemy.HealthBar.Visible = DebugSettings.ShowEnemyHealthBars;
            }
        }
        else if (keyEvent.PhysicalKeycode == Key.F2)
        {
            DebugSettings.ShowFloatingCombatText = !DebugSettings.ShowFloatingCombatText;
        }
    }
}
