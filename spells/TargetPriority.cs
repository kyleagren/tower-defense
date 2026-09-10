// How a ritual picks which enemy in range to cast at. A spell declares its own default
// (Spell.BaseTargetPriority) and a future targeting rune overrides it on the resolved context,
// per README's Towers/Spells/Runes section.
public enum TargetPriority
{
    // Furthest along its route — the conventional tower-defense default, and first here so a
    // context that never sets one still behaves sensibly.
    ClosestToGoal,
    ClosestToRitual
}
