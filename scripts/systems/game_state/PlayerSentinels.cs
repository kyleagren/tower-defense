using System.Collections.Generic;

// Which Sentinel each player is currently playing as - a placeholder for a real hub selection
// screen that doesn't exist yet, and for a real lobby/multiplayer system. Local stands in for
// "which PlayerId this client controls" until that exists too.
//
// The local player's pick isn't stored here at all - it's read straight from
// PlayerSaveData.LastPlayedCharacter, which persists across sessions and already defaults
// sensibly for a brand-new save. A future hub just reads/writes that same field; nothing here
// needs removing once it exists. _selections only exists for other (currently hypothetical)
// players, who'll eventually sync their own picks over the network.
public static class PlayerSentinels
{
    public static PlayerId Local = PlayerId.P1;

    private static readonly Dictionary<PlayerId, CharacterId> _selections = new();

    // A remote player with no synced pick yet falls back to the same default a brand-new save
    // uses, rather than throwing - the selection arrives over the network after the player does.
    public static CharacterId Get(PlayerId player)
    {
        if (player == Local)
        {
            return SaveManager.Instance.Data.LastPlayedCharacter;
        }

        return _selections.TryGetValue(player, out CharacterId character) ? character : CharacterId.Soldier;
    }

    // The eventual Sentinel-selection screen calls this for the local player; a future network
    // sync calls it for remote players.
    public static void Set(PlayerId player, CharacterId character)
    {
        if (player == Local)
        {
            SaveManager.Instance.Data.LastPlayedCharacter = character;
            SaveManager.Instance.Save();
            return;
        }

        _selections[player] = character;
    }
}
