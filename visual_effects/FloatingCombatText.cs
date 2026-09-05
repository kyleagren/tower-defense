using Godot;

// A short-lived, upward-drifting, fading damage number - spawned fresh per hit rather than
// pooled, matching how ImpactFlash/ExplosionVisual already work in this project. Capped rather
// than pooled too: a big enough burst of simultaneous hits (AoE, chain, fork, salvo, all stacked)
// can otherwise leave hundreds of these alive - each with its own Label3D and Tween - for the
// entire second they're visible, which is a real frame-time cost. Evicting the oldest once the cap
// is hit bounds that regardless of how many hits land at once.
public partial class FloatingCombatText : Node3D
{
    private const int MaxActive = 30;

    [Export] public Label3D Label { get; private set; }
    [Export] public float RiseDistance = 1f;
    [Export] public float Duration = 1f;
    [Export] public float RandomHorizontalSpread = 0.3f;

    public override void _EnterTree()
    {
        FloatingCombatTextRegistry.Register(this);

        // Free(), not QueueFree() - eviction needs to shrink the list immediately so this loop
        // actually converges within a single burst. QueueFree()'d nodes stay registered until
        // their deferred removal lands later, so a burst of many hits in one frame would just
        // queue the same still-registered "oldest" node repeatedly instead of evicting several.
        while (FloatingCombatTextRegistry.Active.Count > MaxActive)
        {
            FloatingCombatTextRegistry.Active[0].Free();
        }
    }

    public override void _ExitTree()
    {
        FloatingCombatTextRegistry.Unregister(this);
    }

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
