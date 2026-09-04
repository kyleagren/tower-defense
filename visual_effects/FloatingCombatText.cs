using Godot;

// A short-lived, upward-drifting, fading damage number - spawned fresh per hit rather than
// pooled, matching how ImpactFlash/ExplosionVisual already work in this project.
public partial class FloatingCombatText : Node3D
{
    [Export] public Label3D Label { get; private set; }
    [Export] public float RiseDistance = 1f;
    [Export] public float Duration = 1f;
    [Export] public float RandomHorizontalSpread = 0.3f;

    public void Show(Vector3 worldPosition, float amount, Color color)
    {
        GlobalPosition = worldPosition + new Vector3(
            (float)GD.RandRange(-RandomHorizontalSpread, RandomHorizontalSpread),
            0f,
            (float)GD.RandRange(-RandomHorizontalSpread, RandomHorizontalSpread));

        Label.Text = Mathf.RoundToInt(amount).ToString();
        Label.Modulate = color;

        Vector3 targetPosition = GlobalPosition + Vector3.Up * RiseDistance;

        Tween tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(this, "global_position", targetPosition, Duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(Label, "modulate:a", 0f, Duration).SetDelay(Duration * 0.4f);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
