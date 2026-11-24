using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class EnemySpawner : Node3D
{
    [Export] public string Id { get; private set; }
    [Export] public DefendableObjective Target { get; private set; }
    [Export] public float PreviewHeight = 3f;
    [Export] public int PreviewFontSize = 48;
    [Export] public Color PreviewBackground = new Color(0f, 0f, 0f, 0.6f);
    [Export] public float PreviewPadding = 0.15f;

    public Lane Lane { get; private set; }

    // This level's, kept from _EnterTree - during a level switch, LevelContext.Current is already the
    // incoming level for a frame.
    private LevelContext _level;
    private Label3D _wavePreview;
    private MeshInstance3D _wavePreviewBackground;

    public override void _EnterTree()
    {
        _level = LevelContext.Current;
        _level.EnemySpawners[Id] = this;
    }

    public override void _ExitTree()
    {
        _level.EnemySpawners.Remove(Id);
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
    }

    public override async void _Ready()
    {
        _wavePreview = new Label3D
        {
            Position = Vector3.Up * PreviewHeight,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = PreviewFontSize,
            OutlineSize = PreviewFontSize / 4,
            Visible = false
        };
        AddChild(_wavePreview);

        // Drawn first so the text lands on top - both are transparent, so RenderPriority decides.
        _wavePreview.RenderPriority = 1;

        _wavePreviewBackground = new MeshInstance3D
        {
            Position = _wavePreview.Position,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = PreviewBackground,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
            },
            Visible = false
        };
        AddChild(_wavePreviewBackground);

        // In _Ready, not _EnterTree: the match is created in WaveController's _EnterTree, which may run after ours.
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        OnGamePhaseChanged(_level.Match.Phase);

        await NavigationReady.WaitFor(this, TryBuildLane, $"EnemySpawner '{Id}'");
    }

    // Which way an enemy faces as it steps out - down the lane. Zero until the lane exists.
    public Vector3 InitialFacing => Lane == null ? Vector3.Zero : (Lane.Waypoints[1] - Lane.Waypoints[0]).Normalized();

    // Host only.
    public void Spawn(string enemyName)
    {
        if (Lane == null)
        {
            GD.PushWarning($"EnemySpawner '{Id}': no Lane available, skipping spawn.");
            return;
        }

        _level.EnemyRoster.Spawn(enemyName, this);
    }

    private void OnGamePhaseChanged(GamePhase phase)
    {
        Wave wave = _level.Match.UpcomingWave;
        string text = phase != GamePhase.Combat && wave != null ? DescribeWave(wave) : "";

        _wavePreview.Text = text;
        _wavePreview.Visible = text != "";
        _wavePreviewBackground.Visible = text != "";

        if (text != "")
        {
            _wavePreviewBackground.Mesh = new QuadMesh { Size = PreviewSize(text) };
        }
    }

    // The label's text block in world units, plus padding on every side.
    private Vector2 PreviewSize(string text)
    {
        Font font = _wavePreview.Font ?? ThemeDB.FallbackFont;
        Vector2 pixels = font.GetMultilineStringSize(text, HorizontalAlignment.Center, -1f, _wavePreview.FontSize);

        return pixels * _wavePreview.PixelSize + Vector2.One * PreviewPadding * 2f;
    }

    // One line per enemy type this spawner sends in the wave, in EnemyCatalog's order.
    private string DescribeWave(Wave wave)
    {
        IEnumerable<string> lines = wave.Steps
            .Where(step => step.Spawn != null)
            .SelectMany(step => step.Spawn)
            .Where(group => group.Lane == Id)
            .GroupBy(group => group.Enemy)
            .OrderBy(enemyGroups => EnemyCatalog.IndexOf(enemyGroups.Key))
            .Select(enemyGroups => $"{enemyGroups.Sum(group => group.Count)} {enemyGroups.Key}");

        return string.Join("\n", lines);
    }

    private bool TryBuildLane()
    {
        if (Target == null)
        {
            return false;
        }

        Rid map = GetWorld3D().NavigationMap;
        Vector3[] pathPoints = NavigationServer3D.MapGetPath(map, GlobalPosition, Target.GlobalPosition, true);

        if (pathPoints.Length < 2)
        {
            return false;
        }

        Lane = new Lane(pathPoints);
        return true;
    }
}
