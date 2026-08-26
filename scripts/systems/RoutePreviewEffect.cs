using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class RoutePreviewEffect : Node3D
{
    [Export] public float PulseInterval = 3f;
    [Export] public float TravelDuration = 1.5f;
    [Export] public float SparkleRadius = 0.15f;
    [Export] public Color SparkleColor = new Color(1f, 0.9f, 0.4f);
    [Export] public int TrailSegments = 6;
    [Export] public float TrailSpacing = 0.04f;
    [Export] public float MinTrailScale = 0.25f;
    [Export] public float MinTrailAlpha = 0.05f;

    private const string SpawnerGroup = "enemy_spawners";

    private List<PathFollow3D[]> routes = new List<PathFollow3D[]>();
    private Timer pulseTimer;

    public override async void _Ready()
    {
        await BuildRoutesWithRetry();

        pulseTimer = new Timer { WaitTime = PulseInterval };
        pulseTimer.Timeout += Pulse;
        AddChild(pulseTimer);

        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        OnGamePhaseChanged(WaveController.GamePhase.Build);
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
    }

    public override void _Process(double delta)
    {
        foreach (PathFollow3D[] segments in routes)
        {
            float leadProgress = segments[0].ProgressRatio;

            for (int i = 1; i < segments.Length; i++)
            {
                segments[i].ProgressRatio = Mathf.Max(0f, leadProgress - i * TrailSpacing);
            }
        }
    }

    private void OnGamePhaseChanged(WaveController.GamePhase phase)
    {
        if (phase == WaveController.GamePhase.Build)
        {
            pulseTimer.Start();
            Pulse();
        }
        else
        {
            pulseTimer.Stop();
        }
    }

    private async Task BuildRoutesWithRetry()
    {
        for (int attempt = 1; attempt <= 30; attempt++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            BuildRoutes();

            if (routes.Count > 0)
            {
                return;
            }
        }
    }

    private void BuildRoutes()
    {
        Rid map = GetWorld3D().NavigationMap;

        foreach (Node node in GetTree().GetNodesInGroup(SpawnerGroup))
        {
            if (node is not EnemySpawner spawner || spawner.Target == null)
            {
                continue;
            }

            Vector3[] pathPoints = NavigationServer3D.MapGetPath(map, spawner.GlobalPosition, spawner.Target.GlobalPosition, true);

            if (pathPoints.Length < 2)
            {
                continue;
            }

            Curve3D curve = new Curve3D();
            foreach (Vector3 point in pathPoints)
            {
                curve.AddPoint(point);
            }

            Path3D path = new Path3D { Curve = curve };
            AddChild(path);

            PathFollow3D[] segments = new PathFollow3D[TrailSegments];

            for (int i = 0; i < TrailSegments; i++)
            {
                float t = TrailSegments > 1 ? 1f - (float)i / (TrailSegments - 1) : 1f;
                float scale = Mathf.Lerp(MinTrailScale, 1f, t);
                float alpha = Mathf.Lerp(MinTrailAlpha, 1f, t);

                PathFollow3D segFollower = new PathFollow3D { RotationMode = PathFollow3D.RotationModeEnum.None };
                path.AddChild(segFollower);
                segFollower.AddChild(CreateSparkleMesh(scale, alpha));

                segments[i] = segFollower;
            }

            routes.Add(segments);
        }
    }

    private MeshInstance3D CreateSparkleMesh(float scale, float alpha)
    {
        Color color = SparkleColor;
        color.A = alpha;

        return new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = SparkleRadius * scale, Height = SparkleRadius * scale * 2f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                EmissionEnabled = true,
                Emission = SparkleColor,
                EmissionEnergyMultiplier = 3f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        };
    }

    private void Pulse()
    {
        foreach (PathFollow3D[] segments in routes)
        {
            foreach (PathFollow3D segment in segments)
            {
                segment.ProgressRatio = 0f;
                segment.Visible = true;
            }

            Tween tween = CreateTween();
            tween.TweenProperty(segments[0], "progress_ratio", 1f, TravelDuration);
            tween.TweenCallback(Callable.From(() =>
            {
                foreach (PathFollow3D segment in segments)
                {
                    segment.Visible = false;
                }
            }));
        }
    }
}
