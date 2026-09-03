using Godot;

// Lets the character look at a WorldItem within range and pick it up with pickup_item - same
// look-and-press pattern as RitualInteractor's repair/destroy prompt.
public partial class ItemInteractor : Node
{
    [Export] public PlayerCharacter Character { get; private set; }
    [Export] public float InteractionRange = GameRules.ItemPickupReach;
    [Export] public PackedScene PromptScene { get; private set; }

    public WorldItem TargetedItem { get; private set; }

    private InteractionPrompt _prompt;
    private string _promptText;

    public override void _Ready()
    {
        _prompt = PromptScene.Instantiate<InteractionPrompt>();
        GetTree().CurrentScene.CallDeferred(Node.MethodName.AddChild, _prompt);
        _prompt.HidePrompt();

        string pickupKey = InputPromptHelper.GetActionKeyName("pickup_item");
        _promptText = $"Press {pickupKey} to Pick Up";
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Character.IsActive)
        {
            TargetedItem = null;
            _prompt.HidePrompt();
            return;
        }

        TargetedItem = FindItemAhead();

        if (TargetedItem == null)
        {
            _prompt.HidePrompt();
            return;
        }

        Vector3 promptPosition = TargetedItem.GlobalPosition + Vector3.Up * 0.3f;
        _prompt.ShowPrompt(promptPosition, _promptText);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Character.IsActive || TargetedItem == null)
        {
            return;
        }

        if (@event.IsActionPressed("pickup_item"))
        {
            TargetedItem.PickUp();
            TargetedItem = null;
            _prompt.HidePrompt();
        }
    }

    private WorldItem FindItemAhead()
    {
        Vector3 origin = Character.Camera.GlobalPosition;
        Vector3 end = Character.GetCameraAimPoint(InteractionRange);

        PhysicsDirectSpaceState3D spaceState = Character.GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollisionMask = PhysicsLayers.Items;

        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

        if (result.Count == 0)
        {
            return null;
        }

        return result["collider"].As<Node3D>() as WorldItem;
    }
}
