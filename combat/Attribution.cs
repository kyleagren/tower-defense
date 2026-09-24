using Godot;

// Who a hit came from. Player is whose it counts as - the owner of the Ritual or Sentinel it came
// from, carried through Burns and Health Link mirrors so the credit follows the damage - and null for
// damage no player dealt (an enemy's attack). Source is the node that dealt it (a Ritual, a Sentinel,
// an Enemy); it may have been freed by the time anything reads it, so check before using it.
public readonly struct Attribution
{
    public PlayerId? Player { get; init; }
    public Node Source { get; init; }
}
