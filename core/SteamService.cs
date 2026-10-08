using Godot;

// Steam, through the GodotSteam GDExtension. It has no C# bindings, so calls go to its singleton by
// name. Steam is optional: without the extension, a running Steam client, or a license for the app,
// IsAvailable stays false and the game plays offline.
//
// Co-op goes through a friends-only Steam lobby: the host opens one by inviting, and anyone who joins
// it connects to its owner. The lobby only finds the host - the game itself travels over
// SteamMultiplayerPeer (NetworkSession).
public partial class SteamService : Node
{
    private const uint AppId = 1010909;
    private const int MaxLobbyMembers = 4;

    // Values from Steam's own enums, which GodotSteam passes through unchanged.
    private const int InitOk = 0;               // SteamAPIInitResult.OK
    private const int ResultOk = 1;             // Result.RESULT_OK
    private const int LobbyTypeFriendsOnly = 1; // LobbyType.LOBBY_TYPE_FRIENDS_ONLY
    private const int LobbyEnterSuccess = 1;    // ChatRoomEnterResponse.CHAT_ROOM_ENTER_RESPONSE_SUCCESS

    public static SteamService Instance { get; private set; }

    public bool IsAvailable { get; private set; }

    private GodotObject _steam;
    // 0 while not in a lobby.
    private ulong _lobbyId;
    private bool _creatingLobby;

    // Joining is hub-only, and being in the hub also means a profile is loaded.
    private static bool IsInHub => LevelContext.Current != null && LevelContext.Current.Match == null;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        // Steam's callbacks must keep arriving while the game is paused - an invite can be accepted
        // from the pause menu.
        ProcessMode = ProcessModeEnum.Always;

        if (!Engine.HasSingleton("Steam"))
        {
            GD.Print("SteamService: GodotSteam isn't loaded; playing without Steam.");
            return;
        }

        GodotObject steam = Engine.GetSingleton("Steam");
        Godot.Collections.Dictionary result = steam.Call("steamInitEx", AppId, false).AsGodotDictionary();

        if (result["status"].AsInt32() != InitOk)
        {
            GD.Print($"SteamService: Steam didn't start ({result["verbal"]}); playing without Steam.");
            return;
        }

        _steam = steam;
        IsAvailable = true;
        _steam.Connect("lobby_created", Callable.From<int, ulong>(OnLobbyCreated));
        _steam.Connect("lobby_joined", Callable.From<ulong, int, bool, int>(OnLobbyJoined));
        _steam.Connect("join_requested", Callable.From<ulong, ulong>(OnJoinRequested));
        GD.Print("SteamService: Steam started.");
    }

    public override void _Process(double delta)
    {
        _steam?.Call("run_callbacks");
    }

    // The pause menu's Invite Friends. The first press opens the lobby and starts hosting over Steam;
    // the invite dialog opens once the lobby exists.
    public void InviteFriends()
    {
        if (_lobbyId != 0)
        {
            _steam.Call("activateGameOverlayInviteDialog", _lobbyId);
            return;
        }

        if (_creatingLobby)
        {
            return;
        }

        if (!NetworkSession.Instance.IsOffline)
        {
            GD.PushWarning("SteamService: already in a session that isn't over Steam; can't open a lobby.");
            return;
        }

        _creatingLobby = true;
        _steam.Call("createLobby", LobbyTypeFriendsOnly, MaxLobbyMembers);
    }

    private void OnLobbyCreated(int result, ulong lobbyId)
    {
        _creatingLobby = false;

        if (result != ResultOk)
        {
            GD.PushError($"SteamService: couldn't create a lobby (result {result}).");
            return;
        }

        _lobbyId = lobbyId;

        if (!NetworkSession.Instance.HostOverSteam())
        {
            LeaveLobby();
            return;
        }

        _steam.Call("activateGameOverlayInviteDialog", lobbyId);
    }

    // An accepted invite, or a friend's Join Game from the friends list.
    private void OnJoinRequested(ulong lobbyId, ulong friendId)
    {
        if (!IsInHub || lobbyId == _lobbyId)
        {
            return;
        }

        LeaveLobby();
        _steam.Call("joinLobby", lobbyId);
    }

    // Also raised for the host, on entering the lobby it just created.
    private void OnLobbyJoined(ulong lobbyId, int permissions, bool locked, int response)
    {
        if (response != LobbyEnterSuccess)
        {
            GD.PushWarning($"SteamService: couldn't join the lobby (response {response}).");
            return;
        }

        ulong owner = _steam.Call("getLobbyOwner", lobbyId).AsUInt64();

        if (owner == _steam.Call("getSteamID").AsUInt64())
        {
            return;
        }

        _lobbyId = lobbyId;
        NetworkSession.Instance.JoinOverSteam(owner);
    }

    // NetworkSession calls this whenever its session closes.
    public void LeaveLobby()
    {
        if (_lobbyId == 0)
        {
            return;
        }

        _steam.Call("leaveLobby", _lobbyId);
        _lobbyId = 0;
    }
}
