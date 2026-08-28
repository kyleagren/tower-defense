using Godot;
using System.Threading.Tasks;

public partial class EnemySpawner : Node3D
{
    [Export] public string Id { get; private set; }
    [Export] public Node3D Target { get; private set; }

    private Vector3 _initialDirection = Vector3.Forward;

    public override async void _Ready()
    {
        await ComputeInitialDirectionWithRetry();
    }

    public void Spawn(PackedScene enemyScene)
    {
        Enemy enemy = enemyScene.Instantiate<Enemy>();
        GetParent().AddChild(enemy);
        enemy.GlobalPosition = GlobalPosition;
        enemy.SetInitialFacing(_initialDirection);
        enemy.SetTarget(Target.GlobalPosition);
    }

    private async Task ComputeInitialDirectionWithRetry()
    {
        for (int attempt = 1; attempt <= 30; attempt++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            if (TryComputeInitialDirection())
            {
                return;
            }
        }
    }

    private bool TryComputeInitialDirection()
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

        _initialDirection = (pathPoints[1] - pathPoints[0]).Normalized();
        return true;
    }
}
