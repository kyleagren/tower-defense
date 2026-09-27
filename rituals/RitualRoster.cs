using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Every placed Ritual in the level lives under this node, spawned by the host through Spawner so it
// appears on every machine under the same name. A player places one by asking the host, which prices
// it against the owner's gear, pays for it from the shared economy and spawns it. Only the host runs
// Rituals; everywhere else they're copies.
public partial class RitualRoster : Node
{
    [Export] public MultiplayerSpawner Spawner { get; private set; }
    [Export] public PackedScene RitualScene { get; private set; }

    // Host only: numbers each spawn, so every Ritual's name is unique and identical on every machine.
    private int _spawnCount;

    // What to run once the host confirms a placement this machine asked for - spending a token.
    private readonly Dictionary<int, Action> _awaitingPlacement = new();
    private int _requestCount;

    // Where each other player is about to place, by seat.
    private readonly Dictionary<PlayerId, RemotePlacementGhost> _ghosts = new();

    public override void _Ready()
    {
        LevelContext.Current.RitualRoster = this;
        Spawner.SpawnFunction = new Callable(this, MethodName.BuildRitual);
        NetworkSession.Instance.SeatsChanged += OnSeatsChanged;
    }

    public override void _ExitTree()
    {
        NetworkSession.Instance.SeatsChanged -= OnSeatsChanged;
    }

    // onPlaced runs only once the Ritual has actually been placed.
    public void RequestPlacement(Spell spell, IReadOnlyList<Rune> runes, Transform3D transform, PlacementData placement, Action onPlaced = null)
    {
        Godot.Collections.Array request = new()
        {
            spell.ResourcePath,
            runes.Select(rune => rune.ResourcePath).ToArray(),
            transform,
            placement?.ToNetwork() ?? new Godot.Collections.Array()
        };

        if (NetworkSession.Instance.IsHost)
        {
            if (TryPlace(PlayerProfiles.LocalId, request))
            {
                onPlaced?.Invoke();
            }

            return;
        }

        _requestCount++;

        if (onPlaced != null)
        {
            _awaitingPlacement[_requestCount] = onPlaced;
        }

        RpcId(1, MethodName.ReceivePlacementRequest, _requestCount, request);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceivePlacementRequest(int requestId, Godot.Collections.Array request)
    {
        long sender = Multiplayer.GetRemoteSenderId();
        PlayerId owner = NetworkSession.Instance.SeatOf(sender);
        bool placed = owner != default && TryPlace(owner, request);
        RpcId(sender, MethodName.ReceivePlacementResult, requestId, placed);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceivePlacementResult(int requestId, bool placed)
    {
        if (Multiplayer.GetRemoteSenderId() == 1 && _awaitingPlacement.Remove(requestId, out Action onPlaced) && placed)
        {
            onPlaced();
        }
    }

    // Host only.
    private bool TryPlace(PlayerId owner, Godot.Collections.Array request)
    {
        (Spell spell, List<Rune> runes, Transform3D _, PlacementData placement) = Decode(request);

        if (spell == null)
        {
            return false;
        }

        SpellCastContext context = SpellInstance.ResolvePreview(spell, runes, PlayerProfiles.Get(owner).SelectedSheet, placement);
        EconomyManager economy = EconomyManager.Instance;

        if (!economy.CanAfford(context))
        {
            return false;
        }

        (int manaCost, int capacityCost) = economy.SpendOnRitual(context);
        _spawnCount++;
        Spawner.Spawn(new Godot.Collections.Array { request, (int)owner, manaCost, capacityCost, _spawnCount });
        return true;
    }

    // Spawner's spawn function - runs on every machine, host included, for each Ritual the host
    // spawns.
    private Node BuildRitual(Variant data)
    {
        Godot.Collections.Array args = data.AsGodotArray();
        (Spell spell, List<Rune> runes, Transform3D transform, PlacementData placement) = Decode(args[0].AsGodotArray());

        Ritual ritual = RitualScene.Instantiate<Ritual>();
        ritual.Name = $"Ritual_{args[4].AsInt32()}";
        ritual.Transform = transform;
        ritual.SetOwnerId((PlayerId)args[1].AsInt32());
        ritual.SetCost(args[2].AsInt32(), args[3].AsInt32());
        // Configure needs the Ritual's own nodes, which it finds in _Ready.
        ritual.Connect(Node.SignalName.Ready, Callable.From(() => ritual.Configure(spell, runes, placement)), (uint)ConnectFlags.OneShot);
        return ritual;
    }

    // Moves go out at most this often, and only once something has moved since the last.
    private const ulong GhostSendIntervalMsec = 66;

    private ulong _nextGhostSendMsec;
    private (Vector3, Vector3) _lastSentGhost;

    // This machine's player's placement, shown on every other screen. Only BeginGhost makes a ghost
    // there - moves travel unreliably, and one arriving after HideGhost mustn't bring it back.
    public void BeginGhost()
    {
        _lastSentGhost = default;
        Rpc(MethodName.ReceiveGhostBegun);
    }

    // range is zero for no range ring.
    public void ShowGhost(float range, Vector3 position)
    {
        _lastSentGhost = (position, position);
        Rpc(MethodName.ReceiveGhostShown, range, position);
    }

    public void MoveGhost(Vector3 position)
    {
        if (ShouldSendGhost(position, position))
        {
            Rpc(MethodName.ReceiveGhostMoved, position);
        }
    }

    public void MoveWallGhost(Vector3 start, Vector3 end, float segmentWidth)
    {
        if (ShouldSendGhost(start, end))
        {
            Rpc(MethodName.ReceiveWallGhostMoved, start, end, segmentWidth);
        }
    }

    public void FreezeWallGhost(Vector3 start, Vector3 end, float segmentWidth, float range)
    {
        Rpc(MethodName.ReceiveWallGhostFrozen, start, end, segmentWidth, range);
    }

    public void HideGhost()
    {
        Rpc(MethodName.ReceiveGhostHidden);
    }

    private bool ShouldSendGhost(Vector3 a, Vector3 b)
    {
        ulong now = Time.GetTicksMsec();

        if (now < _nextGhostSendMsec || _lastSentGhost == (a, b))
        {
            return false;
        }

        _nextGhostSendMsec = now + GhostSendIntervalMsec;
        _lastSentGhost = (a, b);
        return true;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveGhostBegun()
    {
        PlayerId owner = NetworkSession.Instance.SeatOf(Multiplayer.GetRemoteSenderId());

        if (owner == default)
        {
            return;
        }

        RemoveGhost(owner);
        RemotePlacementGhost ghost = new RemotePlacementGhost();
        ghost.Initialize(owner);
        AddChild(ghost);
        _ghosts[owner] = ghost;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveGhostShown(float range, Vector3 position)
    {
        SenderGhost()?.ShowRitual(RitualScene, range, position);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void ReceiveGhostMoved(Vector3 position)
    {
        SenderGhost()?.MoveRitual(position);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void ReceiveWallGhostMoved(Vector3 start, Vector3 end, float segmentWidth)
    {
        SenderGhost()?.ShowWall(start, end, segmentWidth);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveWallGhostFrozen(Vector3 start, Vector3 end, float segmentWidth, float range)
    {
        SenderGhost()?.FreezeWall(start, end, segmentWidth, range);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveGhostHidden()
    {
        RemoveGhost(NetworkSession.Instance.SeatOf(Multiplayer.GetRemoteSenderId()));
    }

    private RemotePlacementGhost SenderGhost() =>
        _ghosts.GetValueOrDefault(NetworkSession.Instance.SeatOf(Multiplayer.GetRemoteSenderId()));

    private void OnSeatsChanged()
    {
        foreach (PlayerId seat in _ghosts.Keys.ToList())
        {
            if (!NetworkSession.Instance.IsSeatFilled(seat))
            {
                RemoveGhost(seat);
            }
        }
    }

    private void RemoveGhost(PlayerId seat)
    {
        if (_ghosts.Remove(seat, out RemotePlacementGhost ghost))
        {
            ghost.QueueFree();
        }
    }

    private static (Spell, List<Rune>, Transform3D, PlacementData) Decode(Godot.Collections.Array request)
    {
        Spell spell = LoadResource<Spell>(request[0].AsString());
        List<Rune> runes = request[1].AsStringArray().Select(LoadResource<Rune>).Where(rune => rune != null).ToList();
        return (spell, runes, request[2].AsTransform3D(), PlacementData.FromNetwork(request[3].AsGodotArray()));
    }

    // Only the project's own resources - these paths come from another machine.
    private static T LoadResource<T>(string path) where T : Resource =>
        path.StartsWith("res://") && ResourceLoader.Exists(path) ? ResourceLoader.Load(path) as T : null;
}
