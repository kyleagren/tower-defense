// How a ritual picks which enemy in range to cast at. A spell declares its own default
// (Spell.BaseTargetPriority); a TargetingRune overrides it.
public enum TargetPriority
{
    // Furthest along its route — the conventional tower-defense default, and first here so a
    // context that never sets one still behaves sensibly.
    ClosestToGoal,
    ClosestToRitual,
    FurthestFromRitual,
    HighestMaxHealth,
    LowestHealth,
    // The enemy with the most others around it - the middle of a pack.
    MostEnemiesNearby,
    // Enemies carrying SpellCastContext.TargetStatus (any Status when that's null) first.
    HasStatus,
    // Any enemy in range, rolled fresh on every search.
    Random
}
