using Godot;

// Every enemy in the level lives under this node, spawned by the host through Spawner so each one
// appears on every machine under the same name. Only the host simulates enemies; everywhere else
// they're copies their MultiplayerSynchronizers keep in step.
public partial class EnemyRoster : Node
{
    [Export] public MultiplayerSpawner Spawner { get; private set; }

    // Host only: numbers each spawn, so every enemy's name is unique and identical on every machine -
    // node paths are how sync updates and messages find it.
    private int _spawnCount;

    private LevelContext _level;

    public override void _Ready()
    {
        _level = LevelContext.Current;
        _level.EnemyRoster = this;
        Spawner.SpawnFunction = new Callable(this, MethodName.BuildEnemy);
    }

    // Host only.
    public void Spawn(string enemyName, EnemySpawner lane)
    {
        _spawnCount++;
        float healthMultiplier = 1f + GameRules.EnemyHealthPerExtraPlayer * (NetworkSession.Instance.PlayerCount - 1);
        Spawner.Spawn(new Godot.Collections.Array { enemyName, lane.Id, _spawnCount, healthMultiplier });
    }

    // Spawner's spawn function - runs on every machine, host included, for each enemy the host
    // spawns. Only the host gives it an objective to walk to; a copy has no pathing of its own.
    private Node BuildEnemy(Variant data)
    {
        Godot.Collections.Array args = data.AsGodotArray();
        string enemyName = args[0].AsString();
        EnemySpawner lane = FindLane(args[1].AsString());

        WalkingEnemy enemy = EnemyCatalog.Get(enemyName).Instantiate<WalkingEnemy>();
        enemy.Name = $"{enemyName}_{args[2].AsInt32()}";
        // Before _Ready, which starts the enemy at full health.
        enemy.MaxHealth *= args[3].AsSingle();
        enemy.Position = lane.GlobalPosition;
        enemy.SetInitialFacing(lane.InitialFacing);

        if (NetworkSession.Instance.IsHost)
        {
            enemy.Connect(Node.SignalName.Ready, Callable.From(() => enemy.SetObjective(lane.Target)), (uint)ConnectFlags.OneShot);
        }

        return enemy;
    }

    private EnemySpawner FindLane(string id)
    {
        if (_level.EnemySpawners.TryGetValue(id, out EnemySpawner spawner))
        {
            return spawner;
        }

        throw new System.ArgumentException($"EnemyRoster: no EnemySpawner with id \"{id}\".");
    }
}
