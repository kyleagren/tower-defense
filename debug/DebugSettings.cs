// Display preferences, kept for a future settings menu to switch - nothing toggles them at runtime
// yet. Turning ShowEnemyHealthBars on or off mid-level will also need to update enemies already
// alive, since each one only reads it once, on spawn.
public static class DebugSettings
{
    public static bool ShowEnemyHealthBars = true;
    public static bool ShowWeaponDamageNumbers = true;
    public static bool ShowTowerDamageNumbers = true;
}
