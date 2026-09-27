using System.Collections.Generic;

// Every player in the session, by seat. Session-wide rather than on LevelContext, since a player's
// gear and Sentinel pick outlive any one scene.
public static class PlayerProfiles
{
    // The seat this machine controls, from the network session.
    public static PlayerId LocalId => NetworkSession.Instance.LocalSeat;

    // The seat hosting the session - its ready starts the ready-check countdown. The host always
    // sits in P1.
    public static PlayerId HostId => PlayerId.P1;

    // Whether another player is actually connected - a pause menu can't freeze a game someone else
    // is still playing. An open session with nobody else in it still counts as alone.
    public static bool IsMultiplayer => NetworkSession.Instance.PlayerCount > 1;

    private static readonly Dictionary<PlayerId, PlayerProfile> _profiles = new();

    public static PlayerProfile Local => Get(LocalId);

    // The local profile is backed by the save file. A remote player with no data yet gets an empty
    // profile rather than a throw - their data arrives over the network after they do.
    public static PlayerProfile Get(PlayerId player)
    {
        if (!_profiles.TryGetValue(player, out PlayerProfile profile))
        {
            PlayerSaveData data = player == LocalId ? SaveManager.Instance.Data : new PlayerSaveData();
            profile = new PlayerProfile(player, data);
            _profiles[player] = profile;
        }

        return profile;
    }

    // When this machine's seat changes - joining or leaving a session. Every cached profile was
    // keyed to the old seat, including which one the save file backs.
    public static void Reset()
    {
        _profiles.Clear();
    }

    // A loadout change doesn't say whose it was, and re-resolving a sheet is a handful of
    // dictionary adds, so dropping every cached sheet is cheaper than threading an identity through.
    public static void InvalidateAllSheets()
    {
        foreach (PlayerProfile profile in _profiles.Values)
        {
            profile.InvalidateSheets();
        }
    }
}
