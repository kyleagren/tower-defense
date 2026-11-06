using Godot;

// Owns one seat's body - its Sentinel. There's only ever one body per player: picking a different
// Sentinel swaps it rather than hiding one and showing another, and anything that must outlive a
// body lives on the player's PlayerLevelState instead.
//
// The owning player asks for a body; only the host makes one, through BodySpawner, so each body
// appears on every machine at the same node path with its owner as multiplayer authority.
public partial class SentinelSpawner : Node
{
    [Export] public PlayerId OwnerId { get; private set; } = PlayerId.P1;

    [Export] public PackedScene SoldierScene { get; private set; }
    [Export] public PackedScene TankScene { get; private set; }

    // Spawns into this node.
    [Export] public MultiplayerSpawner BodySpawner { get; private set; }

    private LevelContext _level;
    private PlayerCharacter _current;
    // So a seat-table update arriving before the first body does can't ask for a second one.
    private bool _firstBodyRequested;

    public override void _Ready()
    {
        if (OwnerId == default)
        {
            return;
        }

        // Registered so a station attached to a dynamically-spawned character (which has no
        // scene-time link back to the spawner that made it) can find "my own spawner" by OwnerId.
        _level = LevelContext.Current;
        _level.SentinelSpawners[OwnerId] = this;
        BodySpawner.SpawnFunction = new Callable(this, MethodName.BuildBody);
        NetworkSession.Instance.SeatsChanged += OnSeatsChanged;
        CallDeferred(MethodName.RequestFirstBody);
    }

    public override void _ExitTree()
    {
        if (OwnerId != default)
        {
            NetworkSession.Instance.SeatsChanged -= OnSeatsChanged;
        }
    }

    private void OnSeatsChanged()
    {
        // The host clears a seat its player has left; freeing the body there despawns it everywhere.
        if (NetworkSession.Instance.IsHost && !NetworkSession.Instance.IsSeatFilled(OwnerId))
        {
            if (_current != null && IsInstanceValid(_current))
            {
                _current.QueueFree();
            }

            _current = null;
            return;
        }

        RequestFirstBody();
    }

    // Only the machine that owns this seat asks, once it knows the seat is its own.
    private void RequestFirstBody()
    {
        if (OwnerId != PlayerProfiles.LocalId || _firstBodyRequested)
        {
            return;
        }

        _firstBodyRequested = true;
        RequestBody(PlayerProfiles.Local.SelectedCharacter);
    }

    // Persists the pick (this is "who to fight with" outside the hub too) and swaps the live body
    // to match.
    public void SelectSentinel(CharacterId character)
    {
        PlayerProfiles.Get(OwnerId).SelectCharacter(character);
        RequestBody(character);
    }

    private void RequestBody(CharacterId character)
    {
        if (NetworkSession.Instance.IsHost)
        {
            SpawnBody(character);
        }
        else
        {
            RpcId(1, MethodName.ReceiveBodyRequest, (int)character);
        }
    }

    // Only a seat's own player can change its body.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveBodyRequest(int character)
    {
        if (NetworkSession.Instance.SenderSeat == OwnerId)
        {
            SpawnBody((CharacterId)character);
        }
    }

    // Host only.
    private void SpawnBody(CharacterId character)
    {
        BodySpawner.Spawn(new Godot.Collections.Array { (int)character, NetworkSession.Instance.PeerOf(OwnerId) });
    }

    // BodySpawner's spawn function - runs on every machine, host included, for each body the host
    // spawns.
    private Node BuildBody(Variant data)
    {
        Godot.Collections.Array args = data.AsGodotArray();
        PlayerCharacter character = SceneFor((CharacterId)args[0].AsInt32()).Instantiate<PlayerCharacter>();
        character.SetOwnerId(OwnerId);
        // Before the body enters the tree, so its _Ready already knows whose it is.
        character.SetMultiplayerAuthority((int)args[1].AsInt64());

        // Read now: the outgoing body's despawn can arrive in the same frame as this spawn, and by
        // the time TakeOver runs it may already have left the tree.
        PlayerCharacter.Pose pose = StartingPose();
        // The moment it's in the tree and set up - not a deferred frame later, by which point a
        // body can already have been despawned again.
        character.Connect(Node.SignalName.Ready, Callable.From(() => TakeOver(character, pose)), (uint)ConnectFlags.OneShot);
        return character;
    }

    // Where the new body appears: where the one it replaces stood, or the seat's spawn point for its
    // first.
    private PlayerCharacter.Pose StartingPose()
    {
        if (_current != null && IsInstanceValid(_current) && _current.IsInsideTree())
        {
            return _current.CurrentPose;
        }

        PlayerSpawnPoint point = _level.SpawnPoints[OwnerId];
        return new PlayerCharacter.Pose(point.GlobalPosition, point.GlobalRotation.Y, 0f);
    }

    // Once the new body is in the tree: place it, activate it, and retire the previous one.
    private async void TakeOver(PlayerCharacter character, PlayerCharacter.Pose pose)
    {
        PlayerCharacter previous = _current;
        _current = character;
        character.ApplyPose(pose);
        character.Activate();

        if (previous == null || !IsInstanceValid(previous))
        {
            return;
        }

        // Gone from view and out of the way the moment its replacement takes over, on every machine,
        // rather than whenever the host's removal arrives - which on a slow machine or connection is
        // long enough to see. Kept in the tree until then, since its camera may still be the current one.
        previous.ProcessMode = ProcessModeEnum.Disabled;
        previous.Visible = false;
        previous.CollisionLayer = 0;
        previous.CollisionMask = 0;
        previous.StopSyncing();

        // Only the host frees a body - freeing it there despawns it everywhere.
        if (!NetworkSession.Instance.IsHost)
        {
            return;
        }

        // Hold the outgoing body - and with it the viewport's current camera - until the incoming
        // camera has actually taken over. Freeing it sooner hands the viewport to the incoming
        // camera through Godot's "current camera left the tree" fallback, bypassing the spring
        // arm wait in PlayerCharacter.MakeCameraCurrent. Another player's camera never goes current
        // here, so for their body the wait runs its full length, giving their own machine the same
        // grace before the despawn reaches it.
        for (int frame = 0; frame < 12 && IsInstanceValid(character) && !character.Camera.Current; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (IsInstanceValid(previous))
        {
            previous.QueueFree();
        }
    }

    private PackedScene SceneFor(CharacterId character) => character == CharacterId.Tank ? TankScene : SoldierScene;
}
