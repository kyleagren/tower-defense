using Godot;

// Where a Swirl burst went off: spiral arms in the swirled element's color, swelling out to the
// burst's radius while they turn and fade.
public partial class SwirlVortex : Node3D
{
    [Export] public GeometryInstance3D Disc { get; private set; }
    [Export] public float Lifetime = 0.45f;
    // The disc swells from this share of the radius to all of it.
    [Export] public float StartScale = 0.3f;

    public void Show(Vector3 position, float radius, Color color)
    {
        GlobalPosition = position;
        Disc.Scale = new Vector3(radius * StartScale, 1f, radius * StartScale);
        Disc.SetInstanceShaderParameter("element_color", color);
        Disc.SetInstanceShaderParameter("progress", 0f);

        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(Disc, "scale", new Vector3(radius, 1f, radius), Lifetime)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenMethod(Callable.From<float>(progress => Disc.SetInstanceShaderParameter("progress", progress)), 0f, 1f, Lifetime);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
