using Godot;

// Repair (single press) / destroy (hold to confirm) on whichever Ritual the character is looking
// at - README section 7's contextual world prompt.
public partial class RitualInteractor : LookInteractor
{
    [Export] public float InteractionRange { get; private set; } = GameRules.RitualReach;
    [Export] public float DestroyHoldDuration = 1f;

    private string _promptText;
    private float _destroyHoldTime;

    protected override uint CollisionMask => PhysicsLayers.Rituals;
    protected override float Range => InteractionRange;

    private Ritual TargetedRitual => (Ritual)Target;

    public override void _Ready()
    {
        base._Ready();

        string repairKey = InputPromptHelper.GetActionKeyName("repair_ritual");
        string destroyKey = InputPromptHelper.GetActionKeyName("destroy_ritual");
        _promptText = $"Press {repairKey} to Repair\nHold {destroyKey} to Destroy";
    }

    // The raycast reports the Ritual's StaticBody3D child, not the Ritual itself.
    protected override Node3D ResolveTarget(Node3D collider) => collider?.GetParent() as Ritual;

    // Looking away mid-hold cancels the destroy, per README section 7.
    protected override void OnTargetChanged(Node3D target)
    {
        _destroyHoldTime = 0f;
    }

    protected override void UpdateTarget(Node3D target, float delta)
    {
        if (!Input.IsActionPressed("destroy_ritual"))
        {
            _destroyHoldTime = 0f;
            return;
        }

        _destroyHoldTime += delta;

        if (_destroyHoldTime >= DestroyHoldDuration)
        {
            Ritual ritual = (Ritual)target;
            ClearTarget();
            ritual.Destroy();
        }
    }

    protected override string BuildPromptText(Node3D target)
    {
        if (_destroyHoldTime <= 0f)
        {
            return _promptText;
        }

        int percent = Mathf.RoundToInt(_destroyHoldTime / DestroyHoldDuration * 100f);
        return $"Destroying... {percent}%";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Target == null)
        {
            return;
        }

        if (@event.IsActionPressed("repair_ritual"))
        {
            TargetedRitual.Repair();
        }
    }
}
