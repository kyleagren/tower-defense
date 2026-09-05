using Godot;

// Instantiates only the Sentinel scene matching this owner's current PlayerSentinels selection,
// rather than hardcoding every roster member as an always-present scene child (the old Architect/
// Soldier pattern) - so there's never more than one Sentinel alive per player, and Sentinel.Local/
// Architect.ByOwner never have to guess which of several simultaneous instances is the real one.
public partial class SentinelSpawner : Node
{
    private const string SoldierScenePath = "res://characters/soldier/Soldier.tscn";
    private const string TankScenePath = "res://characters/tank/Tank.tscn";

    [Export] public PlayerId OwnerId { get; private set; } = PlayerId.P1;

    public override void _Ready()
    {
        CharacterId selected = PlayerSentinels.Get(OwnerId);
        string path = ScenePathFor(selected);
        PackedScene scene = path != null ? GD.Load<PackedScene>(path) : null;

        if (scene == null)
        {
            GD.PushError($"SentinelSpawner: no scene registered for {selected}.");
            return;
        }

        Sentinel sentinel = scene.Instantiate<Sentinel>();
        sentinel.SetOwnerId(OwnerId);
        CallDeferred(nameof(SpawnSentinel), sentinel);
    }

    private void SpawnSentinel(Sentinel sentinel)
    {
        GetParent().AddChild(sentinel);
        sentinel.HandoffFrom(Architect.ByOwner[OwnerId]);
    }

    private static string ScenePathFor(CharacterId character) => character switch
    {
        CharacterId.Soldier => SoldierScenePath,
        CharacterId.Tank => TankScenePath,
        _ => null
    };
}
