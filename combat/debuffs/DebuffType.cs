using System;

// Every type of debuff, one bit each - so which ones an enemy carries fits in one number its copies
// can be sent, and a debuff sent to the host can say what it is.
[Flags]
public enum DebuffType
{
    None = 0,
    Burn = 1 << 0,
    Immolation = 1 << 1,
    Chill = 1 << 2,
    ChilledGround = 1 << 3,
    Converged = 1 << 4,
    Taunted = 1 << 5,
    HealthLink = 1 << 6,
    Frostburn = 1 << 7,
    Melted = 1 << 8,
    Overloaded = 1 << 9,
    Frozen = 1 << 10,
    Electrocuted = 1 << 11
}
