using Godot;

// Look at a WorldItem within range and pick it up with pickup_item - the same look-and-press
// pattern RitualInteractor uses, both built on LookInteractor.
public partial class ItemInteractor : LookInteractor
{
    [Export] public float InteractionRange { get; private set; } = GameRules.ItemPickupReach;

    private string _pickupText;
    private string _cachedPromptText;

    protected override uint CollisionMask => PhysicsLayers.Items;
    protected override float Range => InteractionRange;

    private WorldItem TargetedItem => (WorldItem)Target;

    public override void _Ready()
    {
        base._Ready();

        _pickupText = $"Press {InputPromptHelper.GetActionKeyName("pickup_item")} to Pick Up";
    }

    // A WorldItem is its own StaticBody3D, so the collider is already the target.
    protected override Node3D ResolveTarget(Node3D collider) => collider as WorldItem;

    // The prompt embeds the item's full rolled-affix tooltip, which is a string.Format per affix
    // plus a join - built once when the target changes rather than every physics tick.
    protected override void OnTargetChanged(Node3D target)
    {
        _cachedPromptText = target is WorldItem worldItem
            ? $"{_pickupText}\n\n{worldItem.Item.BuildTooltip()}"
            : null;
    }

    protected override string BuildPromptText(Node3D target) => _cachedPromptText;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Target == null)
        {
            return;
        }

        if (@event.IsActionPressed("pickup_item"))
        {
            TargetedItem.PickUp();
            ClearTarget();
        }
    }
}
