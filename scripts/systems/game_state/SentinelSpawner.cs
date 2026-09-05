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

    [Export] public bool ActivateImmediately { get; private set; } = false;
    [Export] public Node3D SpawnPoint { get; private set; }

    private PlayerCharacter _current;

    public override void _Ready()
    {
        if (OwnerId == default)
        {
            return;
        }

        CallDeferred(nameof(RespawnSelected));
    }

    // Despawns whatever Sentinel is currently spawned (if any) and spawns whichever one
    // PlayerSentinels now selects, at the previous one's position - the same entry point serves
    // both the initial spawn and the hub's live character-select swap.
    public void RespawnSelected()
    {
        PlayerCharacter previous = _current;
        SpawnSelected();

        if (previous != null && IsInstanceValid(previous))
        {
            _current.HandoffFrom(previous);
            previous.QueueFree();
        }
    }

    private void SpawnSelected()
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
        GetParent().AddChild(sentinel);
        _current = sentinel;

        if (ActivateImmediately)
        {
            sentinel.GlobalPosition = SpawnPoint.GlobalPosition;
            sentinel.GlobalRotation = new Vector3(0f, SpawnPoint.GlobalRotation.Y, 0f);
            sentinel.ActivateStandalone();
        }
        else
        {
            sentinel.HandoffFrom(Architect.ByOwner[OwnerId]);
        }
    }

    private static string ScenePathFor(CharacterId character) => character switch
    {
        CharacterId.Soldier => SoldierScenePath,
        CharacterId.Tank => TankScenePath,
        _ => null
    };
}
