using Godot;

// Repair (hold to confirm) / destroy (hold to confirm) on whichever Ritual the character is
// looking at - the contextual world prompt in README's Towers/Spells/Runes section.
public partial class RitualInteractor : LookInteractor
{
    [Export] public float InteractionRange { get; private set; } = GameRules.RitualReach;
    [Export] public float RepairHoldDuration = 1f;
    [Export] public float DestroyHoldDuration = 1f;

    private string _promptText;
    private float _repairHoldTime;
    private float _destroyHoldTime;

    protected override uint CollisionMask => PhysicsLayers.Rituals;
    protected override float Range => InteractionRange;

    public override void _Ready()
    {
        base._Ready();

        string repairKey = InputPromptHelper.GetActionKeyName("repair_ritual");
        string destroyKey = InputPromptHelper.GetActionKeyName("destroy_ritual");
        _promptText = $"Hold {repairKey} to Repair\nHold {destroyKey} to Destroy";
    }

    // The raycast reports the Ritual's StaticBody3D child, not the Ritual itself.
    protected override Node3D ResolveTarget(Node3D collider) => collider?.GetParent() as Ritual;

    // Looking away mid-hold cancels either action, per README's Towers/Spells/Runes section.
    protected override void OnTargetChanged(Node3D target)
    {
        _repairHoldTime = 0f;
        _destroyHoldTime = 0f;
    }

    protected override void UpdateTarget(Node3D target, float delta)
    {
        Ritual ritual = (Ritual)target;

        if (Input.IsActionPressed("destroy_ritual"))
        {
            _repairHoldTime = 0f;
            _destroyHoldTime += delta;

            if (_destroyHoldTime >= DestroyHoldDuration)
            {
                ClearTarget();
                ritual.Destroy();
            }

            return;
        }

        _destroyHoldTime = 0f;

        if (Input.IsActionPressed("repair_ritual"))
        {
            _repairHoldTime += delta;

            if (_repairHoldTime >= RepairHoldDuration)
            {
                _repairHoldTime = 0f;
                ritual.Repair();
            }

            return;
        }

        _repairHoldTime = 0f;
    }

    protected override string BuildPromptText(Node3D target)
    {
        if (_destroyHoldTime > 0f)
        {
            int percent = Mathf.RoundToInt(_destroyHoldTime / DestroyHoldDuration * 100f);
            return $"Destroying... {percent}%";
        }

        if (_repairHoldTime > 0f)
        {
            int percent = Mathf.RoundToInt(_repairHoldTime / RepairHoldDuration * 100f);
            return $"Repairing... {percent}%";
        }

        return _promptText;
    }
}
