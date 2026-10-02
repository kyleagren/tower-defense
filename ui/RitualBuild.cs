using System;
using System.Collections.Generic;
using System.Linq;

// The ritual being put together in the construction menu: a spell, what each socket holds, and the
// rules for what can go in a socket and what it all costs. Sockets keep their positions - an empty
// one is null - since tokens and favorites save runes in the sockets they were put in.
public class RitualBuild
{
    private readonly Rune[] _sockets = new Rune[GameRules.MaxRuneSlots];

    public Spell Spell { get; private set; }

    public IReadOnlyList<Rune> Sockets => _sockets;

    public int SocketCount => _sockets.Length;

    public IEnumerable<Rune> Runes => _sockets.Where(rune => rune != null);

    public bool IsEmpty => _sockets.All(rune => rune == null);

    public int ManaCost => ComputeManaCost(Spell, _sockets);

    public Rune RuneIn(int socket) => _sockets[socket];

    public void SetRune(int socket, Rune rune) => _sockets[socket] = rune;

    public void Clear() => Array.Clear(_sockets);

    // Picking the same spell again keeps what's socketed; only a different spell clears it.
    public void ChooseSpell(Spell spell)
    {
        if (spell != Spell)
        {
            Clear();
        }

        Spell = spell;
    }

    public void Load(RuneLoadout loadout)
    {
        Spell = loadout.Spell;

        for (int socket = 0; socket < _sockets.Length; socket++)
        {
            _sockets[socket] = socket < loadout.Runes.Count ? loadout.Runes[socket] : null;
        }
    }

    // The first empty socket after afterSocket, wrapping around, or -1 once every socket is full.
    public int NextEmptySocket(int afterSocket)
    {
        for (int step = 1; step <= _sockets.Length; step++)
        {
            int socket = (afterSocket + step) % _sockets.Length;

            if (_sockets[socket] == null)
            {
                return socket;
            }
        }

        return -1;
    }

    // What the build would cost with one socket holding something else; null for that socket empty.
    public int ManaCostWith(int socket, Rune rune)
    {
        Rune[] runes = (Rune[])_sockets.Clone();
        runes[socket] = rune;
        return ComputeManaCost(Spell, runes);
    }

    public bool ConflictsWithOtherSocket(Rune rune, int socket)
    {
        for (int i = 0; i < _sockets.Length; i++)
        {
            if (i != socket && _sockets[i] != null && _sockets[i].ConflictsWith(rune))
            {
                return true;
            }
        }

        return false;
    }

    public static int ComputeManaCost(Spell spell, IEnumerable<Rune> runes) =>
        SpellInstance.ResolvePreview(spell, runes.Where(rune => rune != null), PlayerProfiles.Local.SelectedSheet).ResolvedManaCost;
}
