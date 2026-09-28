using Godot;
using System.Collections.Generic;
using System.Linq;

// The persistent root scene: holds the current level - the hub counts as one - under
// LevelContainer, and switches it. The host spawns each level through LevelSpawner, so every
// client, including one that joins later, ends up in the same level; a level's own spawners then
// replicate what lives inside it. Anything a level creates at runtime goes under the level
// (LevelContext.LevelRoot), never here, or it would outlive the level. Switching is a short
// handshake: everyone stops sending sync updates for the outgoing level before the host removes it,
// so nothing arrives for a node that's already gone.
public partial class LevelLoader : Node
{
    public static LevelLoader Instance { get; private set; }

    [Export] public Node LevelContainer { get; private set; }
    [Export] public MultiplayerSpawner LevelSpawner { get; private set; }
    [Export] public PackedScene HubScene { get; private set; }

    // How long the host waits for every client to confirm before switching anyway.
    private const float ConfirmTimeout = 2f;

    private string _pendingScenePath;
    private readonly HashSet<long> _awaitingConfirmation = new();
    private SceneTreeTimer _confirmTimer;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        LevelSpawner.SpawnFunction = new Callable(this, MethodName.BuildLevel);
    }

    public void LoadHub()
    {
        Load(HubScene.ResourcePath);
    }

    // Host only - everyone else follows through LevelSpawner.
    public void Load(string scenePath)
    {
        if (!NetworkSession.Instance.IsHost)
        {
            return;
        }

        _pendingScenePath = scenePath;
        _awaitingConfirmation.Clear();
        _awaitingConfirmation.UnionWith(Multiplayer.GetPeers().Select(peer => (long)peer));

        StopSyncingOutgoingLevel();
        Rpc(MethodName.PrepareForLevelChange);

        if (_awaitingConfirmation.Count == 0)
        {
            SwitchLevel();
            return;
        }

        _confirmTimer = GetTree().CreateTimer(ConfirmTimeout);
        _confirmTimer.Timeout += SwitchLevel;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PrepareForLevelChange()
    {
        StopSyncingOutgoingLevel();
        RpcId(1, MethodName.ConfirmReadyForLevelChange);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ConfirmReadyForLevelChange()
    {
        if (_awaitingConfirmation.Remove(Multiplayer.GetRemoteSenderId()) && _awaitingConfirmation.Count == 0)
        {
            SwitchLevel();
        }
    }

    // Everything this machine sends updates for in the outgoing level - its own bodies, and on the
    // host anything else it owns - stops now, while the level still exists everywhere.
    public void StopSyncingOutgoingLevel()
    {
        foreach (Node level in LevelContainer.GetChildren())
        {
            foreach (Node node in level.FindChildren("*", nameof(MultiplayerSynchronizer), owned: false))
            {
                if (node.IsMultiplayerAuthority())
                {
                    node.QueueFree();
                }
            }
        }
    }

    // Runs once per Load, whichever comes first: every confirmation or the timeout. The outgoing
    // level is freed in the same frame the new one arrives; LevelContext is built for that overlap.
    private void SwitchLevel()
    {
        if (_pendingScenePath == null)
        {
            return;
        }

        if (_confirmTimer != null)
        {
            _confirmTimer.Timeout -= SwitchLevel;
            _confirmTimer = null;
        }

        string scenePath = _pendingScenePath;
        _pendingScenePath = null;

        foreach (Node level in LevelContainer.GetChildren())
        {
            level.QueueFree();
        }

        LevelSpawner.Spawn(scenePath);
    }

    // Leaving a session: the level being left is taken out at once, before this machine becomes its
    // own host - otherwise, for the frame it lingered, every copy in it would find itself in charge and
    // start simulating with none of what the real host gave it.
    public void DropLevel()
    {
        foreach (Node level in LevelContainer.GetChildren())
        {
            LevelContainer.RemoveChild(level);
            level.QueueFree();
        }
    }

    private Node BuildLevel(Variant scenePath)
    {
        return GD.Load<PackedScene>(scenePath.AsString()).Instantiate();
    }
}
