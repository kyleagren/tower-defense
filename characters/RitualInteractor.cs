using Godot;

public partial class RitualInteractor : Node
{
    [Export] public PlayerCharacter Character { get; private set; }
    [Export] public float InteractionRange = GameRules.RitualReach;
    [Export] public PackedScene PromptScene { get; private set; }
    [Export] public float DestroyHoldDuration = 1f;

    public Ritual TargetedRitual { get; private set; }

    private InteractionPrompt _prompt;
    private string _promptText;
    private float _destroyHoldTime;

    public override void _Ready()
    {
        _prompt = PromptScene.Instantiate<InteractionPrompt>();
        GetTree().CurrentScene.CallDeferred(Node.MethodName.AddChild, _prompt);
        _prompt.HidePrompt();

        string repairKey = InputPromptHelper.GetActionKeyName("repair_ritual");
        string destroyKey = InputPromptHelper.GetActionKeyName("destroy_ritual");
        _promptText = $"Press {repairKey} to Repair\nHold {destroyKey} to Destroy";
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Character.IsActive)
        {
            TargetedRitual = null;
            _destroyHoldTime = 0f;
            _prompt.HidePrompt();
            return;
        }

        TargetedRitual = FindRitualAhead();

        if (TargetedRitual == null)
        {
            _destroyHoldTime = 0f;
            _prompt.HidePrompt();
            return;
        }

        if (Input.IsActionPressed("destroy_ritual"))
        {
            _destroyHoldTime += (float)delta;

            if (_destroyHoldTime >= DestroyHoldDuration)
            {
                Ritual ritual = TargetedRitual;
                TargetedRitual = null;
                _destroyHoldTime = 0f;
                _prompt.HidePrompt();
                ritual.Destroy();
                return;
            }
        }
        else
        {
            _destroyHoldTime = 0f;
        }

        Vector3 promptPosition = TargetedRitual.GlobalPosition + Vector3.Up * 0.3f;

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
        if (!Character.IsActive || TargetedRitual == null)
        {
            return;
        }

        if (@event.IsActionPressed("repair_ritual"))
        {
            TargetedRitual.Repair();
        }
    }

    private Ritual FindRitualAhead()
    {
        Vector3 origin = Character.Camera.GlobalPosition;
        Vector3 end = Character.GetCameraAimPoint(InteractionRange);

        PhysicsDirectSpaceState3D spaceState = Character.GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollisionMask = PhysicsLayers.Rituals;

        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

        if (result.Count == 0)
        {
            return null;
        }

        Node3D colliderNode = result["collider"].As<Node3D>();
        return colliderNode?.GetParent() as Ritual;
    }
}
