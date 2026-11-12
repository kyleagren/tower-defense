using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// The persistent root scene: holds the current level - the hub counts as one - under
// LevelContainer, and switches it. The host spawns each level through LevelSpawner, so every
// client, including one that joins later, ends up in the same level; a level's own spawners then
// replicate what lives inside it. Anything a level creates at runtime goes under the level
// (LevelContext.LevelRoot), never here, or it would outlive the level.
//
// Switching: everyone covers their screen, stops sending sync updates for the outgoing level and reads
// the next one in on a background thread; the host removes the old level once all of that is done,
// so nothing arrives for a node that's already gone. The first load of a run then warms up every
// effect's shaders. The covers stay up until every machine in the switch has finished, then come
// down together.
public partial class LevelLoader : Node
{
    public static LevelLoader Instance { get; private set; }

    [Export] public Node LevelContainer { get; private set; }
    [Export] public MultiplayerSpawner LevelSpawner { get; private set; }
    [Export] public PackedScene HubScene { get; private set; }
    [Export] public LoadingScreen LoadingScreen { get; private set; }
    [Export] public ShaderWarmupManifest WarmupManifest { get; private set; }

    // How long the host waits for every client to read the next level in before switching anyway. A
    // client that hasn't finished loads it outright when the level arrives.
    private const float ConfirmTimeout = 30f;

    // The bar's share for reading the level in, when a warm-up follows it.
    private const float LoadShareBeforeWarmup = 0.4f;

    private const long HostPeer = 1;

    // From the start of a switch until the covers come down.
    public bool IsLoading { get; private set; }

    private bool WillWarmUp => !ShaderWarmup.HasRun && WarmupManifest != null;

    private string _pendingScenePath;
    private bool _hostPreloaded;
    private readonly HashSet<long> _awaitingConfirmation = new();
    private SceneTreeTimer _confirmTimer;
    private PackedScene _preloadedScene;
    private bool _localLoaded;

    // Host only: every machine in the current switch, and those that have finished loading it.
    private readonly HashSet<long> _expectedLoads = new();
    private readonly HashSet<long> _finishedLoads = new();

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        LevelSpawner.SpawnFunction = new Callable(this, MethodName.BuildLevel);
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
    }

    public void LoadHub()
    {
        Load(HubScene.ResourcePath);
    }

    // Host only - everyone else follows through LevelSpawner.
    public async void Load(string scenePath)
    {
        if (!NetworkSession.Instance.IsHost || _pendingScenePath != null)
        {
            return;
        }

        _pendingScenePath = scenePath;
        await Cover();

        _awaitingConfirmation.Clear();
        _awaitingConfirmation.UnionWith(Multiplayer.GetPeers().Select(peer => (long)peer));
        _hostPreloaded = false;

        StopSyncingOutgoingLevel();
        Rpc(MethodName.PrepareForLevelChange, scenePath);

        if (_awaitingConfirmation.Count > 0)
        {
            _confirmTimer = GetTree().CreateTimer(ConfirmTimeout);
            _confirmTimer.Timeout += SwitchLevel;
        }

        _preloadedScene = await Preload(scenePath);
        _hostPreloaded = true;
        TrySwitchLevel();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private async void PrepareForLevelChange(string scenePath)
    {
        await Cover();
        StopSyncingOutgoingLevel();
        _preloadedScene = await Preload(scenePath);
        RpcId(HostPeer, MethodName.ConfirmReadyForLevelChange);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ConfirmReadyForLevelChange()
    {
        if (_awaitingConfirmation.Remove(Multiplayer.GetRemoteSenderId()))
        {
            TrySwitchLevel();
        }
    }

    private async Task Cover()
    {
        IsLoading = true;
        _localLoaded = false;
        _preloadedScene = null;
        LoadingScreen.Begin("Loading");
        await LoadingScreen.CoverScreen();
    }

    private async Task<PackedScene> Preload(string scenePath)
    {
        float share = WillWarmUp ? LoadShareBeforeWarmup : 1f;
        Godot.Collections.Array progress = new();
        ResourceLoader.LoadThreadedRequest(scenePath);

        while (ResourceLoader.LoadThreadedGetStatus(scenePath, progress) == ResourceLoader.ThreadLoadStatus.InProgress)
        {
            if (progress.Count > 0)
            {
                LoadingScreen.SetProgress(progress[0].AsSingle() * share);
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        LoadingScreen.SetProgress(share);
        return ResourceLoader.LoadThreadedGet(scenePath) as PackedScene;
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

    private void TrySwitchLevel()
    {
        if (_hostPreloaded && _awaitingConfirmation.Count == 0)
        {
            SwitchLevel();
        }
    }

    // Runs once per Load, whichever comes first: everyone ready or the timeout. The outgoing level is
    // freed in the same frame the new one arrives; LevelContext is built for that overlap.
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

        // Before the spawn: on the host the new level is ready inside Spawn itself.
        _expectedLoads.Clear();
        _expectedLoads.Add(HostPeer);
        _expectedLoads.UnionWith(Multiplayer.GetPeers().Select(peer => (long)peer));
        _finishedLoads.Clear();

        LevelSpawner.Spawn(scenePath);
    }

    // Leaving a session: the level being left is taken out at once, before this machine becomes its
    // own host - otherwise, for the frame it lingered, every copy in it would find itself in charge and
    // start simulating with none of what the real host gave it. Any switch in progress is abandoned;
    // the cover stays up for the load that follows.
    public void DropLevel()
    {
        if (_confirmTimer != null)
        {
            _confirmTimer.Timeout -= SwitchLevel;
            _confirmTimer = null;
        }

        _pendingScenePath = null;
        _hostPreloaded = false;
        _localLoaded = false;
        _awaitingConfirmation.Clear();
        _expectedLoads.Clear();
        _finishedLoads.Clear();

        foreach (Node level in LevelContainer.GetChildren())
        {
            LevelContainer.RemoveChild(level);
            level.QueueFree();
        }
    }

    private Node BuildLevel(Variant scenePath)
    {
        string path = scenePath.AsString();

        // Joining mid-level: no switch covered this machine's screen first.
        if (!IsLoading)
        {
            IsLoading = true;
            LoadingScreen.Begin("Loading");
            LoadingScreen.CoverInstantly();
        }

        PackedScene scene = _preloadedScene?.ResourcePath == path ? _preloadedScene : GD.Load<PackedScene>(path);
        _preloadedScene = null;

        Node level = scene.Instantiate();
        level.Ready += () => OnLevelReady(level);
        return level;
    }

    // A frame's wait first, so the level is done setting itself up before anything is added to it.
    private async void OnLevelReady(Node level)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (WillWarmUp && IsInstanceValid(level))
        {
            LoadingScreen.SetStatus("Preparing effects");
            ShaderWarmup warmup = new ShaderWarmup();
            level.AddChild(warmup);
            await warmup.Run(WarmupManifest, progress => LoadingScreen.SetProgress(Mathf.Lerp(LoadShareBeforeWarmup, 1f, progress)));
        }

        if (!IsInstanceValid(level) || !level.IsInsideTree())
        {
            return;
        }

        _localLoaded = true;
        LoadingScreen.SetProgress(1f);
        LoadingScreen.SetStatus("Waiting for players");

        if (NetworkSession.Instance.IsHost)
        {
            MarkLoaded(HostPeer);
        }
        else
        {
            RpcId(HostPeer, MethodName.ReportLevelLoaded);
        }
    }

    // A client that joined after the switch began was never waited on, so it's let in at once.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReportLevelLoaded()
    {
        if (!NetworkSession.Instance.IsHost)
        {
            return;
        }

        long sender = Multiplayer.GetRemoteSenderId();

        if (_expectedLoads.Contains(sender))
        {
            MarkLoaded(sender);
        }
        else
        {
            RpcId(sender, MethodName.ReleaseLevel);
        }
    }

    private void MarkLoaded(long peer)
    {
        _finishedLoads.Add(peer);
        TryReleaseLevel();
    }

    private void OnPeerDisconnected(long peer)
    {
        if (NetworkSession.Instance.IsHost && _expectedLoads.Remove(peer))
        {
            _finishedLoads.Remove(peer);
            TryReleaseLevel();
        }
    }

    private void TryReleaseLevel()
    {
        if (_expectedLoads.Count == 0)
        {
            return;
        }

        if (!_expectedLoads.IsSubsetOf(_finishedLoads))
        {
            Rpc(MethodName.ReceiveLoadCount, _finishedLoads.Count, _expectedLoads.Count);
            return;
        }

        _expectedLoads.Clear();
        _finishedLoads.Clear();
        Rpc(MethodName.ReleaseLevel);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveLoadCount(int finished, int expected)
    {
        if (IsLoading && _localLoaded)
        {
            LoadingScreen.SetStatus($"Waiting for players ({finished}/{expected})");
        }
    }

    // A late joiner let in early also hears the group's release; it's already uncovered by then.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private async void ReleaseLevel()
    {
        if (!IsLoading)
        {
            return;
        }

        IsLoading = false;
        _localLoaded = false;
        await LoadingScreen.UncoverScreen();
    }
}
