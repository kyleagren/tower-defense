// Named aliases for the closed generic registries. EnemyRegistry and IceWallSegmentRegistry were
// two copies of the same twenty lines; these keep the readable call-site names without the
// duplicate implementations. Each closed generic gets its own static list, so the two registries
// stay entirely independent.
global using EnemyRegistry = ActiveRegistry<Enemy>;
global using IceWallSegmentRegistry = ActiveRegistry<IceWallSegment>;
global using FloatingCombatTextRegistry = ActiveRegistry<FloatingCombatText>;
global using RitualRegistry = ActiveRegistry<Ritual>;
global using WorldItemRegistry = ActiveRegistry<WorldItem>;
