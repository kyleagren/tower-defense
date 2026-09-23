using System.Collections.Generic;

// Every player in the session, by seat. Session-wide rather than on LevelContext, since a player's
// gear and Sentinel pick outlive any one scene. LocalId stands in for "which seat this machine
// controls" until a real lobby exists.
public static class PlayerProfiles
{
    public static PlayerId LocalId { get; } = PlayerId.P1;

    // The seat hosting the session - its ready starts the ready-check countdown. P1 until real
    // networking decides who hosts.
    public static PlayerId HostId { get; } = PlayerId.P1;

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
