using Godot;

// One cut across a target, facing the way the strike came from and tilted around that direction.
// The stroke is drawn along the blade from its local -X end, holds, then fades out whole.
public partial class SlashVisual : Node3D
{
    [Export] public MeshInstance3D Blade { get; private set; }
    // How long the stroke takes to cross the blade.
    [Export] public float DrawTime = 0.1f;
    // How long the finished stroke stays before fading.
    [Export] public float HoldTime = 0.25f;
    // 0 vanishes instantly.
    [Export] public float FadeTime = 0.1f;

    // A Ritual never sits directly above its target, so the flattened facing is never zero.
    // extraHold is added to HoldTime, for a slash waiting on later ones to finish.
    public void Show(Vector3 center, Vector3 facing, float tilt, float extraHold)
    {
        GlobalPosition = center;
        GlobalBasis = Basis.LookingAt(new Vector3(facing.X, 0f, facing.Z), Vector3.Up) * new Basis(Vector3.Back, tilt);

        Blade.SetInstanceShaderParameter("head", 0f);

        Tween tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(head => Blade.SetInstanceShaderParameter("head", head)), 0f, 1f, DrawTime);
        tween.TweenInterval(HoldTime + extraHold);
        tween.TweenMethod(Callable.From<float>(opacity => Blade.SetInstanceShaderParameter("opacity", opacity)), 1f, 0f, FadeTime);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
