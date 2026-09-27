using Godot;

// The persistent root scene: holds the current level - the hub counts as one - under
// LevelContainer, and switches it. The host spawns each level through LevelSpawner, so every
// client, including one that joins later, ends up in the same level; a level's own spawners then
// replicate what lives inside it. Anything a level creates at runtime goes under the level
// (LevelContext.LevelRoot), never here, or it would outlive the level.
public partial class LevelLoader : Node
{
    public static LevelLoader Instance { get; private set; }

    [Export] public Node LevelContainer { get; private set; }
    [Export] public MultiplayerSpawner LevelSpawner { get; private set; }
    [Export] public PackedScene HubScene { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        LevelSpawner.SpawnFunction = new Callable(this, MethodName.BuildLevel);

        // A joiner waits for the host's level to arrive rather than loading one of its own.
        if (NetworkSession.Instance.IsHost)
        {
            CallDeferred(MethodName.LoadHub);
        }
    }

    public void LoadHub()
    {
        Load(HubScene.ResourcePath);
    }

    // Host only - everyone else follows through LevelSpawner. The outgoing level is freed in the
    // same frame the new one arrives; LevelContext is built for that overlap.
    public void Load(string scenePath)
    {
        if (!NetworkSession.Instance.IsHost)
        {
            return;
        }

        foreach (Node level in LevelContainer.GetChildren())
        {
            level.QueueFree();
        }

        LevelSpawner.Spawn(scenePath);
    }

    private Node BuildLevel(Variant scenePath)
    {
        return GD.Load<PackedScene>(scenePath.AsString()).Instantiate();
    }
}
