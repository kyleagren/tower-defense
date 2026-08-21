using Godot;

public partial class EnemySpawner : Node3D
{
    [Export] public string Id { get; private set; }
    [Export] public Node3D Target { get; private set; }

    public void Spawn(PackedScene enemyScene)
    {
        Enemy enemy = enemyScene.Instantiate<Enemy>();
        GetParent().AddChild(enemy);
        enemy.GlobalPosition = GlobalPosition;
        enemy.SetTarget(Target.GlobalPosition);
    }
}
