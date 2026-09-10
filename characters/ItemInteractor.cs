using Godot;

// Look at a WorldItem within range and pick it up with interact - the same look-and-press
// pattern RitualInteractor uses, both built on LookInteractor. The actual visual (name, and the
// full tooltip once directly targeted) belongs to ItemNameplateController rather than the
// inherited Label3D world prompt, so this returns an empty prompt instead of duplicating it.
public partial class ItemInteractor : LookInteractor
{
    [Export] public float InteractionRange { get; private set; } = GameRules.ItemPickupReach;

    protected override uint CollisionMask => PhysicsLayers.Items;
    protected override float Range => InteractionRange;

    private WorldItem TargetedItem => (WorldItem)Target;

    // A WorldItem is its own StaticBody3D, so the collider is already the target.
    protected override Node3D ResolveTarget(Node3D collider) => collider as WorldItem;

    // Targets whatever nameplate the crosshair is actually over, instead of raycasting for a small
    // 3D collider that's fiddly to aim at precisely - ItemNameplateController already computes each
    // visible item's on-screen rect every frame to keep nameplates from overlapping each other.
    protected override Node3D FindTargetAhead()
    {
        if (ItemNameplateController.Instance == null)
        {
            return null;
        }

        Vector2 crosshair = GetViewport().GetVisibleRect().Size / 2f;
        return ItemNameplateController.Instance.GetItemAt(crosshair);
    }

    protected override string BuildPromptText(Node3D target) => string.Empty;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Target == null)
        {
            return;
        }

        if (@event.IsActionPressed("interact"))
        {
            TargetedItem.PickUp();
            ClearTarget();
        }
    }
}
