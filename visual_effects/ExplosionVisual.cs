using Godot;

public partial class ExplosionVisual : Node3D, IImpactVisual
{
    [Export] public MeshInstance3D Sphere { get; private set; }
    [Export] public float Lifetime = 0.35f;
    // The blast swells from this share of its radius to all of it.
    [Export] public float StartScale = 0.6f;

    public void Show(Vector3 position, float radius)
    {
        GlobalPosition = position;
        Sphere.Scale = Vector3.One * radius * StartScale;
        Sphere.SetInstanceShaderParameter("progress", 0f);

        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(Sphere, "scale", Vector3.One * radius, Lifetime)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenMethod(Callable.From<float>(progress => Sphere.SetInstanceShaderParameter("progress", progress)), 0f, 1f, Lifetime);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
