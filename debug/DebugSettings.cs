using Godot;

// Display preferences, switched from the pause menu's Options screen. Kept in their own file rather
// than the player save - they belong to this machine, not to a player's progress.
public static class DebugSettings
{
    private const string FilePath = "user://settings.cfg";
    private const string Section = "display";

    public static bool ShowEnemyHealthBars = true;
    public static bool ShowWeaponDamageNumbers = true;
    public static bool ShowRitualDamageNumbers = true;
    public static bool CameraOnLeftShoulder;
    public static bool ScreenShake = true;

    // Runs on first use, after the defaults above, so a missing file (first launch) keeps them.
    static DebugSettings()
    {
        ConfigFile file = new ConfigFile();

        if (file.Load(FilePath) != Error.Ok)
        {
            return;
        }

        ShowEnemyHealthBars = file.GetValue(Section, nameof(ShowEnemyHealthBars), ShowEnemyHealthBars).AsBool();
        ShowWeaponDamageNumbers = file.GetValue(Section, nameof(ShowWeaponDamageNumbers), ShowWeaponDamageNumbers).AsBool();
        ShowRitualDamageNumbers = file.GetValue(Section, nameof(ShowRitualDamageNumbers), ShowRitualDamageNumbers).AsBool();
        CameraOnLeftShoulder = file.GetValue(Section, nameof(CameraOnLeftShoulder), CameraOnLeftShoulder).AsBool();
        ScreenShake = file.GetValue(Section, nameof(ScreenShake), ScreenShake).AsBool();
    }

    public static void Save()
    {
        ConfigFile file = new ConfigFile();
        file.SetValue(Section, nameof(ShowEnemyHealthBars), ShowEnemyHealthBars);
        file.SetValue(Section, nameof(ShowWeaponDamageNumbers), ShowWeaponDamageNumbers);
        file.SetValue(Section, nameof(ShowRitualDamageNumbers), ShowRitualDamageNumbers);
        file.SetValue(Section, nameof(CameraOnLeftShoulder), CameraOnLeftShoulder);
        file.SetValue(Section, nameof(ScreenShake), ScreenShake);

        Error result = file.Save(FilePath);

        if (result != Error.Ok)
        {
            GD.PushError($"DebugSettings: failed to save {FilePath} ({result}).");
        }
    }
}
