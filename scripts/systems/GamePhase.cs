// The match's top-level phase. Lives here rather than nested inside WaveController because most
// consumers (EventBus, both player characters, RitualPlacer, PhaseIndicator) care about the phase
// without caring about wave sequencing.
public enum GamePhase
{
    Build,
    Combat
}
