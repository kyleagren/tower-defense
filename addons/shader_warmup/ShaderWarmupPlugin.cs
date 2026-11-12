#if TOOLS
using Godot;
using System.Collections.Generic;
using System.Linq;

// Project > Tools > Rebuild Shader Warm-up Manifest: lists every 3D scene, material and shader in the
// project for ShaderWarmup to draw behind the first loading screen. Re-run it after adding effects.
[Tool]
public partial class ShaderWarmupPlugin : EditorPlugin
{
    private const string MenuItemName = "Rebuild Shader Warm-up Manifest";

    // Levels compile their own materials as they load, and menus have nothing 3D to warm.
    private static readonly string[] _skippedSceneFolders = ["res://levels/", "res://ui/"];

    public override void _EnterTree() => AddToolMenuItem(MenuItemName, Callable.From(Rebuild));

    public override void _ExitTree() => RemoveToolMenuItem(MenuItemName);

    private static void Rebuild()
    {
        List<string> scenes = new();
        List<string> materials = new();
        List<string> shaders = new();
        Collect(EditorInterface.Singleton.GetResourceFilesystem().GetFilesystem(), scenes, materials, shaders);

        ShaderWarmupManifest manifest = new()
        {
            ScenePaths = scenes.ToArray(),
            MaterialPaths = materials.ToArray(),
            ShaderPaths = shaders.ToArray()
        };

        Error error = ResourceSaver.Save(manifest, ShaderWarmupManifest.FilePath);

        if (error != Error.Ok)
        {
            GD.PushError($"ShaderWarmupPlugin: couldn't save the manifest ({error}).");
            return;
        }

        GD.Print($"Shader warm-up manifest rebuilt: {scenes.Count} scenes, {materials.Count} materials, {shaders.Count} shaders.");
    }

    private static void Collect(EditorFileSystemDirectory directory, List<string> scenes, List<string> materials, List<string> shaders)
    {
        if (directory.GetPath().StartsWith("res://addons/"))
        {
            return;
        }

        for (int i = 0; i < directory.GetFileCount(); i++)
        {
            string path = directory.GetFilePath(i);
            string type = directory.GetFileType(i);

            if (type == nameof(PackedScene))
            {
                if (!_skippedSceneFolders.Any(path.StartsWith))
                {
                    scenes.Add(path);
                }
            }
            else if (ClassDB.IsParentClass(type, nameof(Shader)))
            {
                if (ResourceLoader.Load<Shader>(path) is Shader shader && ShaderWarmup.IsWarmable(shader))
                {
                    shaders.Add(path);
                }
            }
            else if (ClassDB.IsParentClass(type, nameof(Material)))
            {
                if (ResourceLoader.Load<Material>(path) is Material material && ShaderWarmup.IsWarmable(material))
                {
                    materials.Add(path);
                }
            }
        }

        for (int i = 0; i < directory.GetSubdirCount(); i++)
        {
            Collect(directory.GetSubdir(i), scenes, materials, shaders);
        }
    }
}
#endif
