using Godot;

public partial class TowerInteractor : Node
{
    [Export] public PlayerCharacter Character { get; private set; }
    [Export] public float InteractionRange = GameRules.TowerReach;
    [Export] public PackedScene PromptScene { get; private set; }
    [Export] public float DestroyHoldDuration = 1f;

    public Ritual TargetedTower { get; private set; }

    private InteractionPrompt _prompt;
    private string _promptText;
    private float _destroyHoldTime;

    public override void _Ready()
    {
        _prompt = PromptScene.Instantiate<InteractionPrompt>();
        GetTree().CurrentScene.CallDeferred(Node.MethodName.AddChild, _prompt);
        _prompt.HidePrompt();

        string repairKey = InputPromptHelper.GetActionKeyName("repair_tower");
        string destroyKey = InputPromptHelper.GetActionKeyName("destroy_tower");
        _promptText = $"Press {repairKey} to Repair\nHold {destroyKey} to Destroy";
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Character.IsActive)
        {
            TargetedTower = null;
            _destroyHoldTime = 0f;
            _prompt.HidePrompt();
            return;
        }

        TargetedTower = FindTowerAhead();

        if (TargetedTower == null)
        {
            _destroyHoldTime = 0f;
            _prompt.HidePrompt();
            return;
        }

        if (Input.IsActionPressed("destroy_tower"))
        {
            _destroyHoldTime += (float)delta;

            if (_destroyHoldTime >= DestroyHoldDuration)
            {
                Ritual tower = TargetedTower;
                TargetedTower = null;
                _destroyHoldTime = 0f;
                _prompt.HidePrompt();
                tower.Destroy();
                return;
            }
        }
        else
        {
            _destroyHoldTime = 0f;
        }

        Vector3 promptPosition = TargetedTower.GlobalPosition + Vector3.Up * 0.3f;

        if (_destroyHoldTime > 0f)
        {
            int percent = Mathf.RoundToInt(_destroyHoldTime / DestroyHoldDuration * 100f);
            _prompt.ShowPrompt(promptPosition, $"Destroying... {percent}%");
        }
        else
        {
            _prompt.ShowPrompt(promptPosition, _promptText);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Character.IsActive || TargetedTower == null)
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
        Vector3 origin = Character.Camera.GlobalPosition;
        Vector3 end = Character.GetCameraAimPoint(InteractionRange);

        PhysicsDirectSpaceState3D spaceState = Character.GetWorld3D().DirectSpaceState;
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
