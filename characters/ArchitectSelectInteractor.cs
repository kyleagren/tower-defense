using Godot;

// Attached to both Sentinel bodies (SentinelBody.tscn) and Architect (Architect.tscn) - a toggle:
// press it as a Sentinel to become the Architect for DPS testing, press it again as the Architect
// to return to whichever Sentinel is actually selected.
public partial class ArchitectSelectInteractor : LookInteractor
{
    [Export] public float InteractionRange { get; private set; } = GameRules.ItemPickupReach;

    protected override uint CollisionMask => PhysicsLayers.Obstacles;
    protected override float Range => InteractionRange;

    protected override Node3D ResolveTarget(Node3D collider) => collider as ArchitectSelectStation;

    protected override string BuildPromptText(Node3D target) =>
        Character is Architect ? "Press E to return to your Sentinel" : "Press E to test as Architect";

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Target == null)
        {
            return;
        }

        if (@event.IsActionPressed("interact"))
        {
            SentinelSpawner spawner = SentinelSpawner.ByOwner[Character.OwnerId];

            if (Character is Architect)
            {
                spawner.SelectSentinel(PlayerSentinels.Get(Character.OwnerId));
            }
            else
            {
                spawner.SelectArchitect();
            }
        }
    }
}
