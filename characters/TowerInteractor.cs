using Godot;

public partial class TowerInteractor : Node
{
    [Export] public PlayerCharacter Owner { get; private set; }
    [Export] public float InteractionRange = GameRules.TowerReach;
    [Export] public PackedScene PromptScene { get; private set; }
    [Export] public float DestroyHoldDuration = 1f;

    public Ritual TargetedTower { get; private set; }

    private InteractionPrompt prompt;
    private string promptText;
    private float destroyHoldTime;

    public override void _Ready()
    {
        prompt = PromptScene.Instantiate<InteractionPrompt>();
        GetTree().CurrentScene.CallDeferred(Node.MethodName.AddChild, prompt);
        prompt.HidePrompt();

        string repairKey = InputPromptHelper.GetActionKeyName("repair_tower");
        string destroyKey = InputPromptHelper.GetActionKeyName("destroy_tower");
        promptText = $"Press {repairKey} to Repair\nHold {destroyKey} to Destroy";
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Owner.IsActive)
        {
            TargetedTower = null;
            destroyHoldTime = 0f;
            prompt.HidePrompt();
            return;
        }

        TargetedTower = FindTowerAhead();

        if (TargetedTower == null)
        {
            destroyHoldTime = 0f;
            prompt.HidePrompt();
            return;
        }

        if (Input.IsActionPressed("destroy_tower"))
        {
            destroyHoldTime += (float)delta;

            if (destroyHoldTime >= DestroyHoldDuration)
            {
                Ritual tower = TargetedTower;
                TargetedTower = null;
                destroyHoldTime = 0f;
                prompt.HidePrompt();
                tower.Destroy();
                return;
            }
        }
        else
        {
            destroyHoldTime = 0f;
        }

        Vector3 promptPosition = TargetedTower.GlobalPosition + Vector3.Up * 0.3f;

        if (destroyHoldTime > 0f)
        {
            int percent = Mathf.RoundToInt(destroyHoldTime / DestroyHoldDuration * 100f);
            prompt.ShowPrompt(promptPosition, $"Destroying... {percent}%");
        }
        else
        {
            prompt.ShowPrompt(promptPosition, promptText);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Owner.IsActive || TargetedTower == null)
        {
            return;
        }

        if (@event.IsActionPressed("repair_tower"))
        {
            TargetedTower.Repair();
        }
    }

    private Ritual FindTowerAhead()
    {
        Vector3 origin = Owner.GlobalPosition;
        Vector3 forward = -Owner.GlobalTransform.Basis.Z;
        Vector3 end = origin + forward * InteractionRange;

        PhysicsDirectSpaceState3D spaceState = Owner.GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollisionMask = 32; // Towers layer only

        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

        if (result.Count == 0)
        {
            return null;
        }

        Node3D colliderNode = result["collider"].As<Node3D>();
        return colliderNode?.GetParent() as Ritual;
    }
}
