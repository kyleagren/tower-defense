using Godot;
using System.Collections.Generic;

// Everything that belongs to one loaded scene - the hub or a level: the live-instance registries,
// which body each player is controlling, and scene-wide effects like the Weak-Point Visor. A node in
// that scene rather than static state, so it's freed with the scene and a scene change needs no
// manual cleanup. Registrants keep the instance they registered with and unregister from that same
// one, so an outgoing scene's _ExitTree can never reach the incoming scene's lists regardless of the
// order Godot tears one down and builds the other.
public partial class LevelContext : Node
{
    public static LevelContext Current { get; private set; }

    public ActiveRegistry<Enemy> Enemies { get; } = new();
    public ActiveRegistry<IceWallSegment> IceWallSegments { get; } = new();
    public ActiveRegistry<FloatingCombatText> FloatingCombatTexts { get; } = new();
    public ActiveRegistry<Ritual> Rituals { get; } = new();
    public ActiveRegistry<WorldItem> WorldItems { get; } = new();

    public Dictionary<PlayerId, Sentinel> Sentinels { get; } = new();
    public Dictionary<PlayerId, PlayerSpawnPoint> SpawnPoints { get; } = new();
    public Dictionary<PlayerId, SentinelSpawner> SentinelSpawners { get; } = new();

    // Every lane's spawner, by Id. Registered on _EnterTree, so it's complete before any _Ready.
    public Dictionary<string, EnemySpawner> EnemySpawners { get; } = new();

    // The level's match - its phase and whether it's over. Null in a scene with no match (the hub).
    public MatchState Match { get; set; }

    // The level's mana, capacity and gold earned. Unlimited in the hub.
    public EconomyManager Economy { get; set; }

    // Where the host spawns enemies. Null in a scene with no match (the hub).
    public EnemyRoster EnemyRoster { get; set; }

    // Where the host spawns placed Rituals.
    public RitualRoster RitualRoster { get; set; }

    // Where the host spawns items players drop.
    public WorldItemRoster WorldItemRoster { get; set; }

    // Where the host shows what Rituals cast on every machine.
    public SpellEffects SpellEffects { get; set; }

    // The objective PostGame loot sprays from. Null in a scene with no match.
    public DefendableObjective PrimaryObjective { get; set; }

    // Where anything the level creates at runtime goes - projectiles, placed Rituals, dropped items,
    // effects - so it's freed with the level. LevelContext sits directly under the level's root.
    public Node LevelRoot => GetParent();

    // Each player's currently controlled body - their Sentinel or their Architect, never both.
    public Dictionary<PlayerId, PlayerCharacter> ActiveCharacters { get; } = new();

    // View-side convenience for this machine's camera and HUD. Gameplay code takes a PlayerId instead.
    public PlayerCharacter LocalActiveCharacter => ActiveCharacters.GetValueOrDefault(PlayerProfiles.LocalId);

    private readonly Dictionary<PlayerId, PlayerLevelState> _playerStates = new();

    // Made on first use, so every body that asks - whenever it spawns - shares one per player.
    public PlayerLevelState StateOf(PlayerId player)
    {
        if (!_playerStates.TryGetValue(player, out PlayerLevelState state))
        {
            state = new PlayerLevelState();
            _playerStates[player] = state;
        }

        return state;
    }

    // Counted rather than a flag, so two Soldiers running the visor at once don't have the first to
    // end strip the markers the other is still using. Scene-wide rather than per-enemy: an enemy
    // spawning mid-duration reads this in _Ready to expose its own markers.
    private int _weakPointVisorActivations;

    public bool IsWeakPointVisorActive => _weakPointVisorActivations > 0;
    public int WeakPointVisorCount { get; private set; }

    public override void _EnterTree()
    {
        Current = this;
    }

    public override void _ExitTree()
    {
        if (Current == this)
        {
            Current = null;
        }

        // The panel stack holds this scene's Controls; an open panel surviving the change would
        // block combat input in the next scene.
        UiPanelStack.Reset();
    }

    public void ActivateWeakPointVisor(int count)
    {
        _weakPointVisorActivations++;

        // Only the first activation exposes markers - a second would re-roll every enemy's markers
        // out from under the first player.
        if (_weakPointVisorActivations > 1)
        {
            return;
        }

        WeakPointVisorCount = count;

        foreach (Enemy enemy in Enemies.Active)
        {
            enemy.ExposeWeakPoints(count);
        }
    }

    public void DeactivateWeakPointVisor()
    {
        _weakPointVisorActivations--;

        if (_weakPointVisorActivations > 0)
        {
            return;
        }

        foreach (Enemy enemy in Enemies.Active)
        {
            enemy.ClearWeakPoints();
        }
    }
}
