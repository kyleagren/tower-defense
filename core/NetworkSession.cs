using Godot;
using System.Collections.Generic;
using System.Linq;

// The multiplayer session: hosting or joining, and which seat each connected peer holds. The only
// code that knows which MultiplayerPeer carries the game - ENet over localhost/LAN in development,
// Steam at release - so swapping transports touches this file alone. Offline, Godot's default peer
// already makes this machine peer 1, the id a host has, so single-player is a host nobody has
// joined. Development starts a session from launch flags: --host, or --join [address].
public partial class NetworkSession : Node
{
    public const int Port = 24736;

    private const long HostPeerId = 1;
    // The host holds P1, so three more fill the remaining seats.
    private const int MaxClients = 3;
    private const string DefaultJoinAddress = "127.0.0.1";

    public static NetworkSession Instance { get; private set; }

    // On every machine whenever the seat table changes - someone joining or leaving, or a joiner
    // receiving the table from the host.
    [Signal] public delegate void SeatsChangedEventHandler();

    private readonly Dictionary<long, PlayerId> _seatsByPeer = new();

    // Unset (default) on a joiner until the host has assigned it a seat.
    public PlayerId LocalSeat => SeatOf(Multiplayer.GetUniqueId());

    public PlayerId SeatOf(long peerId) => _seatsByPeer.GetValueOrDefault(peerId);

    public bool IsSeatFilled(PlayerId seat) => _seatsByPeer.ContainsValue(seat);

    public IEnumerable<PlayerId> FilledSeats => _seatsByPeer.Values;

    public int PlayerCount => _seatsByPeer.Count;

    // Offline counts as the host, since that's what single-player is.
    public bool IsHost => Multiplayer.IsServer();

    public long PeerOf(PlayerId seat) => _seatsByPeer.FirstOrDefault(pair => pair.Value == seat).Key;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        _seatsByPeer[HostPeerId] = PlayerId.P1;

        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        // Deferred - swapping the peer from inside one of its own signals isn't safe.
        Multiplayer.ConnectionFailed += () => CallDeferred(MethodName.ReturnToSolo);
        Multiplayer.ServerDisconnected += () => CallDeferred(MethodName.ReturnToSolo);

        StartFromLaunchFlags();
    }

    // Read from both lists, so a flag works whether or not it was passed after a "--" separator.
    private void StartFromLaunchFlags()
    {
        List<string> args = OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).ToList();

        if (args.Contains("--host"))
        {
            Host();
            return;
        }

        int joinIndex = args.IndexOf("--join");

        if (joinIndex >= 0)
        {
            bool hasAddress = joinIndex + 1 < args.Count && !args[joinIndex + 1].StartsWith("--");
            Join(hasAddress ? args[joinIndex + 1] : DefaultJoinAddress);
        }
    }

    public void Host()
    {
        ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
        Error error = peer.CreateServer(Port, MaxClients);

        if (error != Error.Ok)
        {
            GD.PushError($"NetworkSession: couldn't host on port {Port} ({error}).");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        GD.Print($"NetworkSession: hosting on port {Port}.");
    }

    public void Join(string address)
    {
        ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
        Error error = peer.CreateClient(address, Port);

        if (error != Error.Ok)
        {
            GD.PushError($"NetworkSession: couldn't join {address}:{Port} ({error}).");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        _seatsByPeer.Clear();
        PlayerProfiles.Reset();
        EmitSignal(SignalName.SeatsChanged);
        GD.Print($"NetworkSession: joining {address}:{Port}.");
    }

    // Host only: a joiner takes the first free seat, and everyone gets the new table.
    private void OnPeerConnected(long peerId)
    {
        if (!IsHost)
        {
            return;
        }

        PlayerId? seat = FirstFreeSeat();

        if (seat == null)
        {
            Multiplayer.MultiplayerPeer.DisconnectPeer((int)peerId);
            return;
        }

        _seatsByPeer[peerId] = seat.Value;
        BroadcastSeats();
    }

    private void OnPeerDisconnected(long peerId)
    {
        if (IsHost && _seatsByPeer.Remove(peerId))
        {
            BroadcastSeats();
        }
    }

    private PlayerId? FirstFreeSeat()
    {
        foreach (PlayerId seat in new[] { PlayerId.P2, PlayerId.P3, PlayerId.P4 })
        {
            if (!_seatsByPeer.ContainsValue(seat))
            {
                return seat;
            }
        }

        return null;
    }

    private void BroadcastSeats()
    {
        long[] peers = _seatsByPeer.Keys.ToArray();
        int[] seats = _seatsByPeer.Values.Select(seat => (int)seat).ToArray();
        Rpc(MethodName.ReceiveSeats, peers, seats);
        EmitSignal(SignalName.SeatsChanged);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveSeats(long[] peers, int[] seats)
    {
        PlayerId previousLocalSeat = LocalSeat;
        _seatsByPeer.Clear();

        for (int i = 0; i < peers.Length; i++)
        {
            _seatsByPeer[peers[i]] = (PlayerId)seats[i];
        }

        if (LocalSeat != previousLocalSeat)
        {
            PlayerProfiles.Reset();
        }

        EmitSignal(SignalName.SeatsChanged);
    }

    // Leaving on purpose, from the pause menu: drop the connection and play on alone. Deferred -
    // this runs from the pause menu, which lives in the level ReturnToSolo tears out.
    public void Leave()
    {
        CallDeferred(MethodName.ReturnToSolo);
    }

    // Losing the host, or never reaching it, drops back to playing alone from the hub.
    private void ReturnToSolo()
    {
        // Can run twice - leaving on purpose may also raise the disconnect it causes.
        if (Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer)
        {
            return;
        }

        LevelLoader.Instance.DropLevel();
        Multiplayer.MultiplayerPeer.Close();
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
        _seatsByPeer.Clear();
        _seatsByPeer[HostPeerId] = PlayerId.P1;
        PlayerProfiles.Reset();
        EmitSignal(SignalName.SeatsChanged);
        LevelLoader.Instance.LoadHub();
    }
}
