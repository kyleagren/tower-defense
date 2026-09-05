using Godot;
using System.Collections.Generic;

// Instantiates only the PlayerCharacter matching this owner's current selection - a Sentinel via
// PlayerSentinels, or the Architect for hub DPS-testing - rather than hardcoding every roster
// member as an always-present scene child. There's never more than one body alive per player, so
// Sentinel.Local/Architect.ByOwner never have to guess which of several simultaneous instances is
// the real one.
public partial class SentinelSpawner : Node
{
    private const string SoldierScenePath = "res://characters/soldier/Soldier.tscn";
    private const string TankScenePath = "res://characters/tank/Tank.tscn";
    private const string ArchitectScenePath = "res://characters/architect/Architect.tscn";

    // Lets a station/trigger attached to a dynamically-spawned character (which has no scene-time
    // link back to the spawner that made it) find "my own spawner" by shared OwnerId.
    public static Dictionary<PlayerId, SentinelSpawner> ByOwner { get; } = new();

    [Export] public PlayerId OwnerId { get; private set; } = PlayerId.P1;

    // The hub has no Architect to hand off from (initially) and no Combat-phase transition to wait
    // for, so its spawner activates the character directly at SpawnPoint instead of the usual
    // Architect-handoff flow a real level uses.
    [Export] public bool ActivateImmediately { get; private set; } = false;
    [Export] public Node3D SpawnPoint { get; private set; }

    private PlayerCharacter _current;

    // Lets a trigger built for one specific transition (e.g. ArchitectTestZone's auto-revert) confirm
    // the body it's reacting to is still the live one before acting - QueueFree()'ing a character can
    // itself look like "leaving the zone" to physics, which would otherwise cause a second, redundant
    // respawn.
    public PlayerCharacter Current => _current;

    public override void _Ready()
    {
        if (OwnerId == default)
        {
            return;
        }

        ByOwner[OwnerId] = this;
        CallDeferred(nameof(SelectPersistedSentinel));
    }

    private void SelectPersistedSentinel()
    {
        SelectSentinel(PlayerSentinels.Get(OwnerId));
    }

    // Persists the pick (this is "who to fight with" outside the hub too) and swaps the live body
    // to match.
    public void SelectSentinel(CharacterId character)
    {
        PlayerSentinels.Set(OwnerId, character);
        Respawn(ScenePathFor(character));
    }

    // Session-local only - testing as Architect in the hub shouldn't overwrite the player's saved
    // Sentinel pick, since that field specifically means "who to fight with," not "who's currently
    // occupying my hub body."
    public void SelectArchitect()
    {
        Respawn(ArchitectScenePath);
    }

    private async void Respawn(string scenePath)
    {
        PlayerCharacter previous = _current;
        PackedScene scene = scenePath != null ? GD.Load<PackedScene>(scenePath) : null;

        if (scene == null)
        {
            GD.PushError($"SentinelSpawner: no scene registered for '{scenePath}'.");
            return;
        }

        PlayerCharacter character = scene.Instantiate<PlayerCharacter>();
        character.SetOwnerId(OwnerId);
        GetParent().AddChild(character);
        _current = character;

        if (ActivateImmediately)
        {
            if (previous != null && IsInstanceValid(previous))
            {
                character.HandoffFrom(previous);
            }
            else
            {
                character.GlobalPosition = SpawnPoint.GlobalPosition;
                character.GlobalRotation = new Vector3(0f, SpawnPoint.GlobalRotation.Y, 0f);
            }

            if (character is Sentinel sentinel)
            {
                sentinel.ActivateStandalone();
            }
        }
        else
        {
            character.HandoffFrom(Architect.ByOwner[OwnerId]);
        }

        if (previous != null && IsInstanceValid(previous))
        {
            // Stop it driving interactors/input immediately, but keep the body itself alive for now.
            previous.ProcessMode = ProcessModeEnum.Disabled;

            // Hold the outgoing body - and with it the viewport's current camera - until the incoming
            // camera has actually taken over. Freeing it sooner hands the viewport to the incoming
            // camera through Godot's "current camera left the tree" fallback, bypassing the spring
            // arm wait in PlayerCharacter.MakeCameraCurrent.
            for (int frame = 0; frame < 12 && IsInstanceValid(character) && !character.Camera.Current; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            if (IsInstanceValid(previous))
            {
                previous.QueueFree();
            }
        }
    }

    private static string ScenePathFor(CharacterId character) => character switch
    {
        CharacterId.Soldier => SoldierScenePath,
        CharacterId.Tank => TankScenePath,
        _ => null
    };
}
