using Godot;

public partial class EnemySpawner : Node3D
{
    [Export] public string Id { get; private set; }
    [Export] public DefendableObjective Target { get; private set; }

    public Lane Lane { get; private set; }

    public override async void _Ready()
    {
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

        LevelContext.Current.EnemyRoster.Spawn(enemyName, this);
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
