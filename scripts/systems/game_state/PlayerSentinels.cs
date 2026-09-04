using System.Collections.Generic;

// Which Sentinel each player is currently playing as - a placeholder for a real hub selection
// screen that doesn't exist yet, and for a real lobby/multiplayer system. Local stands in for
// "which PlayerId this client controls" until that exists too.
//
// TODO: the P1 seed below is a bare hardcoded Soldier because there's no hub yet. Once a hub
// exists, replace it with a persisted "last played character" on PlayerSaveData instead - that's
// a real feature a hub wants anyway (default to whoever you played last), not just a stand-in to
// rip out.
public static class PlayerSentinels
{
    public static PlayerId Local = PlayerId.P1;

    private static readonly Dictionary<PlayerId, CharacterId> _selections = new()
    {
        [PlayerId.P1] = CharacterId.Soldier
    };

    public static CharacterId Get(PlayerId player) => _selections[player];

    // The eventual Sentinel-selection screen calls this to change a player's pick - mirrors how
    // SelectedSentinel.Current used to just be assigned directly.
    public static void Set(PlayerId player, CharacterId character)
    {
        _selections[player] = character;
    }
}
