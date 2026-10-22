using Godot;

// Repair (hold to confirm) / destroy (hold to confirm) on whichever Ritual the character is
// looking at - the contextual world prompt in README's Rituals/Spells/Runes section.
public partial class RitualInteractor : LookInteractor
{
    [Export] public float InteractionRange { get; private set; } = GameRules.RitualReach;
    [Export] public float RepairHoldDuration = 1f;
    [Export] public float DestroyHoldDuration = 1f;

    private float _repairHoldTime;
    private float _destroyHoldTime;

    protected override uint CollisionMask => PhysicsLayers.RitualHitbox;
    protected override float Range => InteractionRange;

    // The raycast reports the Ritual's StaticBody3D child, not the Ritual itself.
    protected override Node3D ResolveTarget(Node3D collider) => collider?.GetParent() as Ritual;

    // Looking away mid-hold cancels either action, per README's Rituals/Spells/Runes section.
    protected override void OnTargetChanged(Node3D target)
    {
        _repairHoldTime = 0f;
        _destroyHoldTime = 0f;
    }

    protected override void UpdateTarget(Node3D target, float delta)
    {
        // D-pad up is also token 2 while R1 is held.
        if (InputChords.TokenModifierHeld)
        {
            _repairHoldTime = 0f;
            _destroyHoldTime = 0f;
            return;
        }

        Ritual ritual = (Ritual)target;

        if (Input.IsActionPressed("destroy_ritual"))
        {
            _repairHoldTime = 0f;
            _destroyHoldTime += delta;

            if (_destroyHoldTime >= DestroyHoldDuration)
            {
                ClearTarget();
                ritual.Health.Destroy(Character.OwnerId);
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
                ritual.Health.Repair(Character.OwnerId);
            }

            return;
        }

        _repairHoldTime = 0f;
    }

    private static readonly Color _unaffordableColor = new Color(1f, 0.3f, 0.3f);

    // Only while there's something to repair - an undamaged Ritual shows no cost at all.
    protected override (string Text, Color Color) BuildPromptDetail(Node3D target)
    {
        int cost = ((Ritual)target).Health.RepairCost;

        if (cost <= 0)
        {
            return (null, Colors.White);
        }

        if (Character is Architect)
        {
            return ("Repair: Free", Colors.White);
        }

        Color color = LevelContext.Current.Economy.CanAffordMana(cost) ? Colors.White : _unaffordableColor;
        return ($"Repair: {cost} Mana", color);
    }

    // Manual activation: a press, not a hold, since firing it is the whole point of walking over.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (Target is Ritual ritual && ritual.IsManual && InputChords.IsPressedWithoutAbilityModifier(@event, "interact"))
        {
            ritual.Activate();
            GetViewport().SetInputAsHandled();
        }
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

        Ritual ritual = (Ritual)target;
        string activate = !ritual.IsManual ? ""
            : ritual.Readiness.IsReady ?$"Press {InputPromptHelper.GetPrompt("interact")} to Activate\n"
            : "Recharging\n";

        return activate
            + $"Hold {InputPromptHelper.GetPrompt("repair_ritual")} to Repair\n"
            + $"Hold {InputPromptHelper.GetPrompt("destroy_ritual")} to Destroy";
    }
}
