using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Draws everything in the warm-up manifest a few items per frame, in front of whichever camera is
// current, so each shader's pipelines compile behind the first loading screen of a run instead of the
// first time an effect appears mid-wave. Scenes never enter the tree - only bare copies of their
// meshes, particles and materials do - so none of their scripts run.
public partial class ShaderWarmup : Node3D
{
    private const int ItemsPerFrame = 4;
    private const float DistanceFromCamera = 3f;

    // Pipelines stay compiled for the rest of the run.
    public static bool HasRun { get; private set; }

    private static readonly BoxMesh _sampleMesh = new();

    private Camera3D _fallbackCamera;

    public static bool IsWarmable(Shader shader) => shader.GetMode() is Shader.Mode.Spatial or Shader.Mode.Particles;

    public static bool IsWarmable(Material material) => material switch
    {
        BaseMaterial3D or ParticleProcessMaterial => true,
        ShaderMaterial shaderMaterial => shaderMaterial.Shader != null && IsWarmable(shaderMaterial.Shader),
        _ => false
    };

    public override void _Process(double delta) => FollowCamera();

    public async Task Run(ShaderWarmupManifest manifest, Action<float> reportProgress)
    {
        TopLevel = true;
        List<Func<Node3D>> items = ListItems(manifest);
        Node3D shownBatch = null;

        for (int start = 0; start < items.Count; start += ItemsPerFrame)
        {
            FollowCamera();
            int end = Math.Min(start + ItemsPerFrame, items.Count);
            Node3D batch = new() { Position = Vector3.Forward * DistanceFromCamera };

            for (int i = start; i < end; i++)
            {
                Node3D item = items[i]();

                if (item != null)
                {
                    batch.AddChild(item);
                }
            }

            AddChild(batch);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            if (!IsStillRunning())
            {
                return;
            }

            // Each batch stays up for two drawn frames, for particles that only draw once they've emitted.
            shownBatch?.QueueFree();
            shownBatch = batch;
            reportProgress((float)end / items.Count);
        }

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

        if (!IsStillRunning())
        {
            return;
        }

        HasRun = true;
        QueueFree();
    }

    // False once the level it's warming up in has been dropped.
    private bool IsStillRunning() => IsInstanceValid(this) && IsInsideTree();

    // Falls back to a camera of its own while the level has none yet, so frames still get drawn.
    private void FollowCamera()
    {
        Camera3D camera = GetViewport().GetCamera3D();

        if (camera == null)
        {
            _fallbackCamera = new Camera3D();
            AddChild(_fallbackCamera);
            _fallbackCamera.MakeCurrent();
        }
        else if (camera != _fallbackCamera)
        {
            GlobalTransform = camera.GlobalTransform;
        }
    }

    private static List<Func<Node3D>> ListItems(ShaderWarmupManifest manifest)
    {
        List<Func<Node3D>> items = new();

        foreach (string path in manifest.ScenePaths)
        {
            items.Add(() => CopyScene(path));
        }

        foreach (string path in manifest.MaterialPaths)
        {
            items.Add(() => Load<Material>(path) is Material material ? Display(material) : null);
        }

        foreach (string path in manifest.ShaderPaths)
        {
            items.Add(() => Load<Shader>(path) is Shader shader ? Display(new ShaderMaterial { Shader = shader }) : null);
        }

        return items;
    }

    // The manifest can name files moved or deleted since it was last rebuilt.
    private static T Load<T>(string path) where T : Resource
    {
        if (ResourceLoader.Exists(path))
        {
            return ResourceLoader.Load<T>(path);
        }

        GD.PushWarning($"ShaderWarmup: \"{path}\" no longer exists - rebuild the warm-up manifest.");
        return null;
    }

    private static Node3D CopyScene(string path)
    {
        PackedScene scene = Load<PackedScene>(path);

        if (scene == null)
        {
            return null;
        }

        Node instance = scene.Instantiate();
        Node3D copies = new();
        CopyVisuals(instance, copies);
        instance.Free();
        return copies;
    }

    private static void CopyVisuals(Node node, Node3D copies)
    {
        switch (node)
        {
            case MeshInstance3D mesh when mesh.Mesh != null:
                copies.AddChild(CopyMesh(mesh));
                break;
            case GpuParticles3D particles:
                copies.AddChild(CopyParticles(particles));
                break;
            case GeometryInstance3D geometry when geometry.MaterialOverride != null && IsWarmable(geometry.MaterialOverride):
                copies.AddChild(Display(geometry.MaterialOverride));
                break;
        }

        // Materials a script holds for meshes it builds itself.
        foreach (Godot.Collections.Dictionary property in node.GetPropertyList())
        {
            if ((property["usage"].AsInt32() & (int)PropertyUsageFlags.ScriptVariable) == 0)
            {
                continue;
            }

            Variant value = node.Get(property["name"].AsStringName());

            if (value.VariantType == Variant.Type.Object && value.AsGodotObject() is Material material && IsWarmable(material))
            {
                copies.AddChild(Display(material));
            }
        }

        foreach (Node child in node.GetChildren())
        {
            CopyVisuals(child, copies);
        }
    }

    private static MeshInstance3D CopyMesh(MeshInstance3D mesh)
    {
        MeshInstance3D copy = new()
        {
            Mesh = mesh.Mesh,
            MaterialOverride = mesh.MaterialOverride,
            MaterialOverlay = mesh.MaterialOverlay,
            CastShadow = mesh.CastShadow
        };

        for (int i = 0; i < mesh.GetSurfaceOverrideMaterialCount(); i++)
        {
            copy.SetSurfaceOverrideMaterial(i, mesh.GetSurfaceOverrideMaterial(i));
        }

        return copy;
    }

    private static GpuParticles3D CopyParticles(GpuParticles3D particles)
    {
        GpuParticles3D copy = new()
        {
            ProcessMaterial = particles.ProcessMaterial,
            MaterialOverride = particles.MaterialOverride,
            TrailEnabled = particles.TrailEnabled,
            TransformAlign = particles.TransformAlign,
            CastShadow = particles.CastShadow,
            DrawPasses = particles.DrawPasses,
            Amount = 1
        };

        for (int i = 0; i < particles.DrawPasses; i++)
        {
            copy.SetDrawPassMesh(i, particles.GetDrawPassMesh(i));
        }

        return copy;
    }

    private static Node3D Display(Material material)
    {
        bool isParticleProcess = material is ParticleProcessMaterial
            || material is ShaderMaterial { Shader: { } shader } && shader.GetMode() == Shader.Mode.Particles;

        return isParticleProcess
            ? new GpuParticles3D { ProcessMaterial = material, DrawPass1 = _sampleMesh, Amount = 1 }
            : new MeshInstance3D { Mesh = _sampleMesh, MaterialOverride = material };
    }
}
