using Godot;

// Single look-and-press interactor for every Obstacles-layer world station. Adding a new station
// type never needs a new interactor class - it just implements IInteractable.
public partial class StationInteractor : LookInteractor
{
    [Export] public float InteractionRange { get; private set; } = GameRules.ItemPickupReach;

    protected override uint CollisionMask => PhysicsLayers.Obstacles;
    protected override float Range => InteractionRange;

    private IInteractable TargetedStation => Target as IInteractable;

    protected override Node3D ResolveTarget(Node3D collider) => collider is IInteractable ? collider : null;

    protected override string BuildPromptText(Node3D target) => TargetedStation.GetPromptText(Character);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Target == null)
        {
            return;
        }

        if (InputChords.IsPressedWithoutAbilityModifier(@event, "interact"))
        {
            TargetedStation.Interact(Character);
        }
    }
}
