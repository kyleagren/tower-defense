using Godot;
using System.Collections.Generic;
using System.Linq;

// Graphics options for slower machines, switched from the pause menu's Options screen. Their own
// file rather than DebugSettings', which rewrites its file whole on every save.
public static class VideoSettings
{
    private const string FilePath = "user://video.cfg";
    private const string Section = "video";

    private static readonly int[] _commonHeights = { 2160, 1440, 1080, 900, 720, 540 };

    public static bool Glow = true;
    public static bool AmbientOcclusion = true;
    public static bool Shadows = true;
    // 0 for the window's own.
    public static int ResolutionHeight;

    // Runs on first use, after the defaults above, so a missing file (first launch) keeps them.
    static VideoSettings()
    {
        ConfigFile file = new ConfigFile();

        if (file.Load(FilePath) != Error.Ok)
        {
            return;
        }

        Glow = file.GetValue(Section, nameof(Glow), Glow).AsBool();
        AmbientOcclusion = file.GetValue(Section, nameof(AmbientOcclusion), AmbientOcclusion).AsBool();
        Shadows = file.GetValue(Section, nameof(Shadows), Shadows).AsBool();
        ResolutionHeight = file.GetValue(Section, nameof(ResolutionHeight), ResolutionHeight).AsInt32();
    }

    public static void Save()
    {
        ConfigFile file = new ConfigFile();
        file.SetValue(Section, nameof(Glow), Glow);
        file.SetValue(Section, nameof(AmbientOcclusion), AmbientOcclusion);
        file.SetValue(Section, nameof(Shadows), Shadows);
        file.SetValue(Section, nameof(ResolutionHeight), ResolutionHeight);

        Error result = file.Save(FilePath);

        if (result != Error.Ok)
        {
            GD.PushError($"VideoSettings: failed to save {FilePath} ({result}).");
        }
    }

    private static Vector2I WindowSize => DisplayServer.WindowGetSize();

    // The window's own height first, then every common one below it.
    public static List<int> ResolutionChoices()
    {
        int native = WindowSize.Y;
        return _commonHeights.Where(height => height < native).Prepend(native).ToList();
    }

    // At the window's aspect ratio.
    public static Vector2I SizeFor(int height)
    {
        Vector2I window = WindowSize;
        return new Vector2I(Mathf.RoundToInt(height * window.X / (float)window.Y), height);
    }

    // Godot's fullscreen never changes the monitor's mode, so a lower resolution is the 3D world
    // rendered smaller and upscaled; the UI always draws at full sharpness. The environment and
    // lights are the level's own nodes, so this runs on every level load as well as on a change.
    public static void Apply(LevelContext level)
    {
        Viewport root = level.GetTree().Root;
        int native = WindowSize.Y;
        bool lowered = ResolutionHeight > 0 && ResolutionHeight < native;

        root.Scaling3DScale = lowered ? ResolutionHeight / (float)native : 1f;
        root.Scaling3DMode = lowered ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;

        // Every level authors glow, ambient occlusion and shadows on, so these only ever take them
        // away. The environment is one resource shared by every level.
        foreach (Node node in level.LevelRoot.GetChildren())
        {
            if (node is WorldEnvironment world && world.Environment != null)
            {
                world.Environment.GlowEnabled = Glow;
                world.Environment.SsaoEnabled = AmbientOcclusion;
            }
            else if (node is DirectionalLight3D light)
            {
                light.ShadowEnabled = Shadows;
            }
        }
    }
}
