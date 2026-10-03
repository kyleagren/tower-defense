using Godot;

// Immolate's eruption at one enemy: a column of flame shooting up out of it, a ring of fire racing out
// across the ground to the eruption's edge, a spray of embers, and a scorch left behind to fade.
public partial class ImmolateEruption : Node3D
{
    [Export] public MeshInstance3D Column { get; private set; }
    [Export] public MeshInstance3D Ground { get; private set; }
    [Export] public GpuParticles3D Embers { get; private set; }
    [Export] public float ColumnHeight { get; private set; } = 4f;
    // The column's radius as a share of the eruption's.
    [Export] public float ColumnWidth { get; private set; } = 0.3f;
    [Export] public float RiseTime { get; private set; } = 0.15f;
    [Export] public float BurnTime { get; private set; } = 0.6f;
    [Export] public float WaveTime { get; private set; } = 0.5f;
    [Export] public float ScorchTime { get; private set; } = 4f;

    // How far below an enemy's center of mass to look for the ground it stood on.
    private const float GroundSearchDepth = 4f;
    // A touch above Vent's, Retaliation's and Immolate's own channel circles.
    private const float GroundOffset = 0.11f;
    // Past 1 so the fire carries on off the edge and burns out rather than stopping at it.
    private const float WaveEnd = 1.3f;

    public void Show(Vector3 center, float radius)
    {
        GlobalPosition = FindGround(center);

        Ground.Position = Vector3.Up * GroundOffset;
        Ground.Scale = new Vector3(radius, 1f, radius);
        SetGround(0f, 0f);

        float width = radius * ColumnWidth;
        SetColumnHeight(width, 0f);
        Column.SetInstanceShaderParameter("fade", 0f);

        Embers.Restart();

        Tween tween = CreateTween().SetParallel();
        tween.TweenMethod(Callable.From<float>(height => SetColumnHeight(width, height)), 0f, ColumnHeight, RiseTime)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenMethod(Callable.From<float>(fade => Column.SetInstanceShaderParameter("fade", fade)), 0f, 1f, BurnTime)
            .SetDelay(RiseTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenMethod(Callable.From<float>(wave => Ground.SetInstanceShaderParameter("wave", wave)), 0f, WaveEnd, WaveTime)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenMethod(Callable.From<float>(fade => Ground.SetInstanceShaderParameter("scorch_fade", fade)), 0f, 1f, ScorchTime)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }

    // The column is a unit cylinder, so it's raised by half its height to keep its base on the ground.
    private void SetColumnHeight(float width, float height)
    {
        Column.Scale = new Vector3(width, Mathf.Max(height, 0.01f), width);
        Column.Position = Vector3.Up * height / 2f;
    }

    private void SetGround(float wave, float scorchFade)
    {
        Ground.SetInstanceShaderParameter("wave", wave);
        Ground.SetInstanceShaderParameter("scorch_fade", scorchFade);
    }

    private Vector3 FindGround(Vector3 center)
    {
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(center, center + Vector3.Down * GroundSearchDepth, PhysicsLayers.World);
        Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count > 0 ? (Vector3)hit["position"] : center;
    }
}
