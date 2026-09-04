// A placeholder for "which seat in the match" until real multiplayer exists - a plain per-slot id,
// not tied to any platform account. Explicit values so default(PlayerId) (0) reads as clearly
// unset rather than colliding with a real player.
public enum PlayerId
{
    P1 = 1,
    P2 = 2,
    P3 = 3,
    P4 = 4
}
